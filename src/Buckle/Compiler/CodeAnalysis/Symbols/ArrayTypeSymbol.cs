using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// An array type symbol.
/// </summary>
internal abstract partial class ArrayTypeSymbol : TypeSymbol {
    private ArrayTypeSymbol(TypeWithAnnotations elementType, NamedTypeSymbol array) {
        elementTypeWithAnnotations = elementType;
        baseType = array;
    }

    internal static ArrayTypeSymbol CreateMDArray(
        TypeWithAnnotations elementType,
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds,
        NamedTypeSymbol array) {
        if (sizes.IsDefaultOrEmpty && lowerBounds.IsDefault)
            return new MDArrayNoSizesOrBounds(elementType, rank, array);

        return new MDArrayWithSizesAndBounds(elementType, rank, sizes, lowerBounds, array);
    }

    internal static ArrayTypeSymbol CreateMDArray(
        TypeWithAnnotations elementType,
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds) {
        return CreateMDArray(elementType, rank, sizes, lowerBounds, CorLibrary.GetSpecialType(SpecialType.Array));
    }

    internal static ArrayTypeSymbol CreateSZArray(TypeWithAnnotations elementType, NamedTypeSymbol array) {
        return new SZArray(elementType, array);
    }

    internal static ArrayTypeSymbol CreateSZArray(TypeWithAnnotations elementType) {
        return new SZArray(elementType, CorLibrary.GetSpecialType(SpecialType.Array));
    }

    public override SymbolKind kind => SymbolKind.ArrayType;

    internal override TypeKind typeKind => TypeKind.Array;

    internal override Symbol containingSymbol => null;

    internal override SyntaxReference syntaxReference => null;

    internal abstract int rank { get; }

    internal abstract bool isSZArray { get; }

    internal abstract bool hasDefaultSizesAndLowerBounds { get; }

    internal virtual ImmutableArray<int> sizes => [];

    internal virtual ImmutableArray<int> lowerBounds => [];

    internal TypeWithAnnotations elementTypeWithAnnotations { get; }

    internal TypeSymbol elementType => elementTypeWithAnnotations.type;

    internal override NamedTypeSymbol baseType { get; }

    internal override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return [];
    }

    internal override Accessibility accessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal bool HasSameShapeAs(ArrayTypeSymbol other) {
        return rank == other.rank && isSZArray == other.isSZArray;
    }

    internal bool HasSameSizesAndLowerBoundsAs(ArrayTypeSymbol other) {
        if (sizes.SequenceEqual(other.sizes)) {
            var thisLowerBounds = lowerBounds;

            if (thisLowerBounds.IsDefault)
                return other.lowerBounds.IsDefault;

            var otherLowerBounds = other.lowerBounds;

            return !otherLowerBounds.IsDefault && thisLowerBounds.SequenceEqual(otherLowerBounds);
        }

        return false;
    }

    internal override bool Equals(TypeSymbol other, TypeCompareKind comparison) {
        return Equals(other as ArrayTypeSymbol, comparison);
    }

    private bool Equals(ArrayTypeSymbol other, TypeCompareKind comparison) {
        if (ReferenceEquals(this, other))
            return true;

        if ((object)other is null || !other.HasSameShapeAs(this) ||
            !other.elementTypeWithAnnotations.Equals(elementTypeWithAnnotations, comparison)) {
            return false;
        }

        if ((comparison & TypeCompareKind.IgnoreArraySizesAndLowerBounds) == 0 && !HasSameSizesAndLowerBoundsAs(other))
            return false;

        return true;
    }

    public override int GetHashCode() {
        var hash = 0;
        TypeSymbol current = this;

        while (current.typeKind == TypeKind.Array) {
            var cur = (ArrayTypeSymbol)current;
            hash = Hash.Combine(cur.rank, hash);
            current = cur.elementType;
        }

        return Hash.Combine(current, hash);
    }
}
