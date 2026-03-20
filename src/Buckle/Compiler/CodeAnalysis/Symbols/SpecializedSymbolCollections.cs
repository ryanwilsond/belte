using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class SpecializedSymbolCollections {
    public static PooledHashSet<TSymbol> GetPooledSymbolHashSetInstance<TSymbol>() where TSymbol : Symbol {
        var instance = PooledSymbolHashSet<TSymbol>.PoolInstance.Allocate();
        return instance;
    }

    public static PooledDictionary<KSymbol, V> GetPooledSymbolDictionaryInstance<KSymbol, V>() where KSymbol : Symbol {
        var instance = PooledSymbolDictionary<KSymbol, V>.PoolInstance.Allocate();
        return instance;
    }
}
