using System;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class LookupSymbolsInfo : AbstractLookupSymbolsInfo<Symbol> {
    private static readonly ObjectPool<LookupSymbolsInfo> Pool
        = new ObjectPool<LookupSymbolsInfo>(() => new LookupSymbolsInfo(), 64);

    private LookupSymbolsInfo() : base(StringComparer.Ordinal) { }

    internal void Free() {
        Clear();
        Pool.Free(this);
    }

    internal static LookupSymbolsInfo GetInstance() {
        return Pool.Allocate();
    }
}
