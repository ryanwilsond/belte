using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeParameterBounds {
    internal static readonly TypeParameterBounds Unset = new TypeParameterBounds();

    internal TypeParameterBounds(
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        ImmutableArray<NamedTypeSymbol> interfaces,
        NamedTypeSymbol effectiveBaseClass,
        TypeSymbol deducedBaseType) {
        this.constraintTypes = constraintTypes;
        this.interfaces = interfaces;
        this.effectiveBaseClass = effectiveBaseClass;
        this.deducedBaseType = deducedBaseType;
    }

    private TypeParameterBounds() {
        effectiveBaseClass = null;
        deducedBaseType = null;
    }

    internal ImmutableArray<TypeWithAnnotations> constraintTypes { get; }

    internal ImmutableArray<NamedTypeSymbol> interfaces { get; }

    internal NamedTypeSymbol effectiveBaseClass { get; }

    internal TypeSymbol deducedBaseType { get; }
}
