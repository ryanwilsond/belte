using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace BelteLspServer;

internal sealed class CompilationManager {
    private const string CompilationName = "Lsp Compilation";
    private static readonly CompilationOptions Options
        = new CompilationOptions(Buckle.BuildMode.None, enableOutput: false);

    private readonly ILogger<CompilationManager> _logger;
    private readonly Dictionary<DocumentUri, (IScopedConfiguration, SyntaxTree)> _documents = [];
    private readonly object _lock = new();
    private readonly Compilation _corLibrary;

    public CompilationManager(ILogger<CompilationManager> logger) {
        _logger = logger;
        _corLibrary = LibraryHelpers.LoadLibraries();
        _logger.Log(LogLevel.Debug, $"Created CorLibrary: {_corLibrary}");
    }

    internal Compilation compilation { get; private set; }

    internal void NewDocument(DocumentUri uri, IScopedConfiguration config, SyntaxTree tree) {
        lock (_lock) {
            compilation = Compilation.Create(CompilationName, Options, compilation ?? _corLibrary, tree);
            _documents[uri] = (config, tree);
        }
    }

    internal void UpdateDocument(DocumentUri uri, TextChange[] textChanges) {
        lock (_lock) {
            var pair = _documents[uri];
            var newTree = pair.Item2;

            foreach (var change in textChanges) {
                _logger.Log(LogLevel.Debug, $"Translated change: '{pair.Item2.text.ToString(change.span)}' -> '{change.newText}'");
                newTree = newTree.WithChanges(change);
            }

            _documents[uri] = (pair.Item1, newTree);
            _logger.Log(LogLevel.Debug, $"Full text: ```{newTree.text}");
            compilation = Compilation.Create(CompilationName, Options, compilation ?? _corLibrary, newTree);
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
