
namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ArrayTypeSymbol {
    private sealed class MDArrayNoSizesOrBounds : MDArray {
        internal MDArrayNoSizesOrBounds(TypeWithAnnotations elementType, int rank, NamedTypeSymbol array)
            : base(elementType, rank, array) { }

        internal override bool hasDefaultSizesAndLowerBounds => true;

        private protected override ArrayTypeSymbol WithElementTypeCore(TypeWithAnnotations elementTypeWithAnnotations) {
            return new MDArrayNoSizesOrBounds(elementTypeWithAnnotations, rank, baseType);
        }
    }
}
