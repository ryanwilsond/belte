
namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ArrayTypeSymbol {
    private abstract class MDArray : ArrayTypeSymbol {
        internal MDArray(TypeWithAnnotations elementType, int rank, NamedTypeSymbol array) : base(elementType, array) {
            this.rank = rank;
        }

        internal sealed override int rank { get; }

        internal sealed override bool isSZArray => false;
    }
}
