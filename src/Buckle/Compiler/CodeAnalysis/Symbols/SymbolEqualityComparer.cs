using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SymbolEqualityComparer : IEqualityComparer<Symbol> {
    internal static readonly SymbolEqualityComparer Default = new SymbolEqualityComparer(TypeCompareKind.IgnoreNullability);
    internal static readonly SymbolEqualityComparer IncludeNullability = new SymbolEqualityComparer(TypeCompareKind.ConsiderEverything);

    internal SymbolEqualityComparer(TypeCompareKind compareKind) {
        this.compareKind = compareKind;
    }

    internal TypeCompareKind compareKind { get; }

    public bool Equals(Symbol x, Symbol y) {
        if (x is null)
            return y is null;

        return x.Equals(y, this);
    }

    public int GetHashCode(Symbol obj) {
        return obj?.GetHashCode() ?? 0;
    }
}
