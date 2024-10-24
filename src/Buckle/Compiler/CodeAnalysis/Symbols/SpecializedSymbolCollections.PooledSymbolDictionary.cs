using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class SpecializedSymbolCollections {
    private static class PooledSymbolDictionary<TSymbol, V> where TSymbol : Symbol {
        internal static readonly ObjectPool<PooledDictionary<TSymbol, V>> PoolInstance
            = PooledDictionary<TSymbol, V>.CreatePool(SymbolEqualityComparer.ConsiderEverything);
    }
}
