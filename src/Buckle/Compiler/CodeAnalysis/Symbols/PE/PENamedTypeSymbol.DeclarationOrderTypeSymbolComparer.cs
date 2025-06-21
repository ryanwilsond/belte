using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class PENamedTypeSymbol {
    private class DeclarationOrderTypeSymbolComparer : IComparer<Symbol> {
        public static readonly DeclarationOrderTypeSymbolComparer Instance = new DeclarationOrderTypeSymbolComparer();

        private DeclarationOrderTypeSymbolComparer() { }

        public int Compare(Symbol x, Symbol y) {
            return HandleComparer.Default.Compare(((PENamedTypeSymbol)x).handle, ((PENamedTypeSymbol)y).handle);
        }
    }
}
