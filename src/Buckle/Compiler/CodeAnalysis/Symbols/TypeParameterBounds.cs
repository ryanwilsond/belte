using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeParameterBounds {
    internal static readonly TypeParameterBounds Unset = new TypeParameterBounds();

    internal TypeParameterBounds(
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        NamedTypeSymbol effectiveBaseClass,
        TypeSymbol deducedBaseType) {
        this.constraintTypes = constraintTypes;
        this.effectiveBaseClass = effectiveBaseClass;
        this.deducedBaseType = deducedBaseType;
    }

    private TypeParameterBounds() {
        constraintTypes = [];
        effectiveBaseClass = null;
        deducedBaseType = null;
    }

    internal ImmutableArray<TypeWithAnnotations> constraintTypes { get; }

    internal NamedTypeSymbol effectiveBaseClass { get; }

    internal TypeSymbol deducedBaseType { get; }

    internal bool IsSet() {
        return this != Unset;
    }
}
