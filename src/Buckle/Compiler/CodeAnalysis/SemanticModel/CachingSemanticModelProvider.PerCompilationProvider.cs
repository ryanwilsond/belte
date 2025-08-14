using System;
using System.Collections.Concurrent;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal sealed partial class CachingSemanticModelProvider {
    private sealed class PerCompilationProvider {
        private readonly Compilation _compilation;
        private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _semanticModelsMap;
        private readonly Func<SyntaxTree, SemanticModel> _createSemanticModel;

        internal PerCompilationProvider(Compilation compilation) {
            _compilation = compilation;
            _semanticModelsMap = new ConcurrentDictionary<SyntaxTree, SemanticModel>();
            _createSemanticModel = tree => compilation.CreateSemanticModel(tree);
        }

        internal SemanticModel GetSemanticModel(SyntaxTree tree) {
            return _semanticModelsMap.GetOrAdd(tree, _createSemanticModel);
        }

        internal void ClearCachedSemanticModel(SyntaxTree tree) {
            _semanticModelsMap.TryRemove(tree, out _);
        }
    }
}
