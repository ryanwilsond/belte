using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal class LexicalOrderSymbolComparer : IComparer<Symbol> {
    public static readonly LexicalOrderSymbolComparer Instance = new LexicalOrderSymbolComparer();

    private LexicalOrderSymbolComparer() { }

    public int Compare(Symbol x, Symbol y) {
        int comparison;

        if (x == y)
            return 0;

        var xSortKey = x.GetLexicalSortKey();
        var ySortKey = y.GetLexicalSortKey();

        comparison = LexicalSortKey.Compare(xSortKey, ySortKey);

        if (comparison != 0)
            return comparison;

        comparison = x.kind.ToSortOrder() - y.kind.ToSortOrder();

        if (comparison != 0)
            return comparison;

        comparison = string.CompareOrdinal(x.name, y.name);
        return comparison;
    }
}
