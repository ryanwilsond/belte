using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace BelteLspServer;

internal class SemanticTokensHandler : SemanticTokensHandlerBase {
    private readonly CompilationManager _manager;
    private readonly ILogger<SemanticTokensHandler> _logger;

    public SemanticTokensHandler(ILogger<SemanticTokensHandler> logger, CompilationManager manager) {
        _logger = logger;
        _manager = manager;
    }

    protected override async Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken) {
        await Task.Yield();

        var tree = _manager.GetTree(identifier.TextDocument.Uri);

        if (tree is null)
            return;

        _manager.compilation.GetSemanticModel()

        TokenizeNode(builder, syntaxTree.GetRoot());

    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken) {
        return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities) {
        return new SemanticTokensRegistrationOptions {
            DocumentSelector = TextDocumentSelector.ForLanguage("belte"),
            Legend = new SemanticTokensLegend {
                TokenTypes = capability.TokenTypes,
                TokenModifiers = capability.TokenModifiers,
            },
            Full = new SemanticTokensCapabilityRequestFull {
                Delta = true
            },
            Range = true
        };
    }
}
