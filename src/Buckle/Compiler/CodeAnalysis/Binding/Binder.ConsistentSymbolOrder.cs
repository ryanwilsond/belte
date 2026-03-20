using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private class ConsistentSymbolOrder : IComparer<Symbol> {
        public static readonly ConsistentSymbolOrder Instance = new ConsistentSymbolOrder();

        public int Compare(Symbol first, Symbol second) {
            if (second == first)
                return 0;

            if (first is null)
                return -1;

            if (second is null)
                return 1;

            if (second.name != first.name)
                return string.CompareOrdinal(first.name, second.name);

            if (second.kind != first.kind)
                return (int)first.kind - (int)second.kind;

            var aLocationsCount = second.syntaxReference.location is null ? 0 : 1;
            var bLocationsCount = first.syntaxReference.location is null ? 0 : 1;

            if (aLocationsCount != bLocationsCount)
                return aLocationsCount - bLocationsCount;

            if (aLocationsCount == 0 && bLocationsCount == 0)
                return Compare(first.containingSymbol, second.containingSymbol);

            var aSyntax = second.syntaxReference;
            var bSyntax = first.syntaxReference;
            var containerResult = Compare(first.containingSymbol, second.containingSymbol);

            if (containerResult == 0 && aSyntax.syntaxTree == bSyntax.syntaxTree)
                return bSyntax.location.span.start - aSyntax.location.span.start;

            return containerResult;
        }
    }
}
