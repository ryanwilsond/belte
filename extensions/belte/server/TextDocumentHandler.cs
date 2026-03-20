using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using MediatR;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace BelteLspServer;

internal sealed class TextDocumentHandler : TextDocumentSyncHandlerBase {
    private readonly ILogger<TextDocumentHandler> _logger;
    private readonly CompilationManager _manager;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly ILanguageServerFacade _server;

    private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
        new TextDocumentFilter {
            Pattern = "**/*.blt"
        }
    );

    public TextDocumentHandler(
        ILogger<TextDocumentHandler> logger,
        ILanguageServerFacade server,
        ILanguageServerConfiguration configuration,
        CompilationManager manager) {
        _logger = logger;
        _server = server;
        _configuration = configuration;
        _manager = manager;
    }

    public TextDocumentSyncKind change { get; } = TextDocumentSyncKind.Incremental;

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken token) {
        await Task.Yield();

        var config = await _configuration.GetScopedConfiguration(request.TextDocument.Uri, token).ConfigureAwait(false);
        var tree = SyntaxTree.Parse(request.TextDocument.Text);

        _manager.NewDocument(request.TextDocument.Uri, config, tree);
        ValidateAndSendDiagnostics(request.TextDocument.Uri);

        return Unit.Value;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken token) {
        if (_configuration.TryGetScopedConfiguration(request.TextDocument.Uri, out var disposable))
            disposable.Dispose();

        _manager.RemoveDocument(request.TextDocument.Uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) {
        return Unit.Task;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) {
        await Task.Yield();

        _manager.UpdateDocument(request.TextDocument.Uri, TranslateChanges(request.ContentChanges, _manager.GetText(request.TextDocument.Uri)));
        ValidateAndSendDiagnostics(request.TextDocument.Uri);

        return Unit.Value;
    }

    private TextChange[] TranslateChanges(Container<TextDocumentContentChangeEvent> changes, SourceText text) {
        var builder = ArrayBuilder<TextChange>.GetInstance();

        foreach (var change in changes) {
            var start = text.GetLine(change.Range.Start.Line).start + change.Range.Start.Character;
            _logger.Log(LogLevel.Debug, $"Raw change: ({start}, len {change.RangeLength}) -> '{change.Text}'");
            builder.Add(
                new TextChange(
                    new TextSpan(start, change.RangeLength),
                    change.Text
                )
            );
        }

        return builder.ToArrayAndFree();
    }

    private DiagnosticSeverity TranslateSeverity(Diagnostics.DiagnosticSeverity severity) {
        return severity switch {
            Diagnostics.DiagnosticSeverity.Fatal => DiagnosticSeverity.Error,
            Diagnostics.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            Diagnostics.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            Diagnostics.DiagnosticSeverity.Info => DiagnosticSeverity.Information,
            Diagnostics.DiagnosticSeverity.Debug => DiagnosticSeverity.Information,
            _ => DiagnosticSeverity.Information,
        };
    }

    private void ValidateAndSendDiagnostics(DocumentUri uri) {
        var belteDiagnostics = _manager.compilation.GetDiagnostics();
        var diagnostics = new List<Diagnostic>();

        while (belteDiagnostics.Count > 0) {
            var diagnostic = belteDiagnostics.Pop();
            var location = diagnostic.location;

            if (location is null)
                continue;

            diagnostics.Add(new Diagnostic {
                Range = new Range(
                    new Position(location.startLine, location.startCharacter),
                    new Position(location.endline, location.endCharacter)
                ),
                Severity = TranslateSeverity(diagnostic.info.severity),
                Message = diagnostic.message,
                Source = "Buckle"
            });
        }

        _logger.Log(LogLevel.Debug, $"Sending {diagnostics.Count} diagnostics");

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams {
            Uri = uri,
            Diagnostics = diagnostics
        });
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) {
        return new TextDocumentSyncRegistrationOptions() {
            DocumentSelector = _textDocumentSelector,
            Change = change,
            Save = new SaveOptions() { IncludeText = true }
        };
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
        return new TextDocumentAttributes(uri, "belte");
    }
}
