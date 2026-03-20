using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal sealed partial class CachingSemanticModelProvider : SemanticModelProvider {
    private static readonly ConditionalWeakTable<Compilation, PerCompilationProvider>.CreateValueCallback CreateProviderCallback
        = new ConditionalWeakTable<Compilation, PerCompilationProvider>.CreateValueCallback(compilation => new PerCompilationProvider(compilation));

    private readonly ConditionalWeakTable<Compilation, PerCompilationProvider> _providerCache;

    internal CachingSemanticModelProvider() {
        _providerCache = [];
    }

    internal override SemanticModel GetSemanticModel(SyntaxTree tree, Compilation compilation)
        => _providerCache.GetValue(compilation, CreateProviderCallback).GetSemanticModel(tree);

    internal void ClearCache(SyntaxTree tree, Compilation compilation) {
        if (_providerCache.TryGetValue(compilation, out var provider))
            provider.ClearCachedSemanticModel(tree);
    }

    internal void ClearCache(Compilation compilation) {
        _providerCache.Remove(compilation);
    }
}
