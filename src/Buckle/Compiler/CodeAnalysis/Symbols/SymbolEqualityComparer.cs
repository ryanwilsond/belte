using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SymbolEqualityComparer : IEqualityComparer<Symbol> {
    internal static readonly SymbolEqualityComparer IgnoringNullable
        = new SymbolEqualityComparer(TypeCompareKind.IgnoreNullability);
    internal static readonly SymbolEqualityComparer ConsiderEverything
        = new SymbolEqualityComparer(TypeCompareKind.ConsiderEverything);
    internal static readonly SymbolEqualityComparer Default = IgnoringNullable;

    internal SymbolEqualityComparer(TypeCompareKind compareKind) {
        this.compareKind = compareKind;
    }

    internal TypeCompareKind compareKind { get; }

    public bool Equals(Symbol x, Symbol y) {
        return x is null ? y is null : x.Equals(y, compareKind);
    }

    public int GetHashCode(Symbol obj) {
        return obj?.GetHashCode() ?? 0;
    }
}
