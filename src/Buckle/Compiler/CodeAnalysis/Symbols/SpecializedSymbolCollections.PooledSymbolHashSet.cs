using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class SpecializedSymbolCollections {
    private static class PooledSymbolHashSet<TSymbol> where TSymbol : Symbol {
        internal static readonly ObjectPool<PooledHashSet<TSymbol>> PoolInstance
            = PooledHashSet<TSymbol>.CreatePool(SymbolEqualityComparer.ConsiderEverything);
    }
}
