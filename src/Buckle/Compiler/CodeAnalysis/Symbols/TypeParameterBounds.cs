
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeParameterBounds {
    internal static readonly TypeParameterBounds Unset = new TypeParameterBounds();

    internal TypeParameterBounds(
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        NamedTypeSymbol effectiveBaseClass) {
        this.constraintTypes = constraintTypes;
        this.effectiveBaseClass = effectiveBaseClass;
    }

    private TypeParameterBounds() {
        constraintTypes = [];
        effectiveBaseClass = null;
    }

    internal ImmutableArray<TypeWithAnnotations> constraintTypes { get; }

    internal NamedTypeSymbol effectiveBaseClass { get; }

    internal bool IsSet() {
        return this != Unset;
    }
}
