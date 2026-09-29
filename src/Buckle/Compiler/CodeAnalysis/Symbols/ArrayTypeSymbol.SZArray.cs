using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ArrayTypeSymbol {
    private sealed class SZArray : ArrayTypeSymbol {
        // TODO interfaces: eventually we will want to put things like IEnumerable here if building with .NET references
#pragma warning disable CS0649
        private readonly ImmutableArray<NamedTypeSymbol> _interfaces;
#pragma warning restore CS0649

        internal SZArray(TypeWithAnnotations elementType, NamedTypeSymbol array) : base(elementType, array) { }

        internal override int rank => 1;

        internal override bool isSZArray => true;

        internal override bool hasDefaultSizesAndLowerBounds => true;

        private protected override ArrayTypeSymbol WithElementTypeCore(TypeWithAnnotations newElementType) {
            return new SZArray(newElementType, baseType);
        }

        internal override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved = null) {
            return _interfaces;
        }
    }
}
