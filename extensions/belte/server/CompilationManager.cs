using System.Collections.Generic;
using System.Threading;
using Buckle;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace BelteLspServer;

internal sealed class CompilationManager {
    private const string CompilationName = "Lsp Compilation";
    private static readonly CompilationOptions Options
        = new CompilationOptions(BuildMode.None, enableOutput: false, references: Compiler.ResolveLibraryLevel(2, false));

    private readonly ILogger<CompilationManager> _logger;
    private readonly Dictionary<DocumentUri, (IScopedConfiguration, SyntaxTree)> _documents = [];
    private readonly Lock _lock = new();

    internal CompilationManager(ILogger<CompilationManager> logger) {
        _logger = logger;
    }

    internal Compilation compilation { get; private set; }

    internal void NewDocument(DocumentUri uri, IScopedConfiguration config, SyntaxTree tree) {
        lock (_lock) {
            compilation = Compilation.Create(CompilationName, Options, compilation, tree);
            _documents[uri] = (config, tree);
        }
    }

    internal void UpdateDocument(DocumentUri uri, TextChange[] textChanges) {
        lock (_lock) {
            var pair = _documents[uri];
            var newTree = pair.Item2;

            foreach (var change in textChanges) {
                // _logger.Log(LogLevel.Debug, $"Translated change: '{pair.Item2.text.ToString(change.span)}' -> '{change.newText}'");
                newTree = newTree.WithChanges(change);
            }

            _documents[uri] = (pair.Item1, newTree);
            // _logger.Log(LogLevel.Debug, $"Full text: ```{newTree.text}");
            compilation = Compilation.Create(CompilationName, Options, compilation, newTree);
        }
    }

    internal void RemoveDocument(DocumentUri uri) {
        lock (_lock)
            _documents.Remove(uri);
    }

    internal SyntaxTree GetTree(DocumentUri uri) {
        return _documents[uri].Item2;
    }

    internal SourceText GetText(DocumentUri uri) {
        return _documents[uri].Item2.text;
    }
}
