
namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ArrayTypeSymbol {
    private sealed class SZArray : ArrayTypeSymbol {
        internal SZArray(TypeWithAnnotations elementType, NamedTypeSymbol array) : base(elementType, array) { }

        internal override int rank => 1;

        internal override bool isSZArray => true;

        internal override bool hasDefaultSizesAndLowerBounds => true;
    }
}
