using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ArrayTypeSymbol {
    private sealed class MDArrayWithSizesAndBounds : MDArray {
        internal MDArrayWithSizesAndBounds(
            TypeWithAnnotations elementType,
            int rank,
            ImmutableArray<int> sizes,
            ImmutableArray<int> lowerBounds,
            NamedTypeSymbol array) : base(elementType, rank, array) {
            this.sizes = sizes;
            this.lowerBounds = lowerBounds;
        }

        internal override ImmutableArray<int> sizes { get; }

        internal override ImmutableArray<int> lowerBounds { get; }

        internal override bool hasDefaultSizesAndLowerBounds => false;
    }
}
