using System;
using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type wrapper of a template parameter replaced after the template is created.
/// </summary>
internal abstract class TemplateParameterSymbol : TypeSymbol {
    public sealed override SymbolKind kind => SymbolKind.TemplateParameter;

    internal sealed override TypeKind typeKind => TypeKind.TemplateParameter;

    internal sealed override Accessibility accessibility => Accessibility.NotApplicable;

    internal sealed override NamedTypeSymbol baseType => null;

    internal sealed override bool isStatic => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isSealed => false;

    internal abstract TypeWithAnnotations underlyingType { get; }

    internal abstract TemplateParameterKind templateParameterKind { get; }

    internal abstract int ordinal { get; }

    internal abstract bool hasPrimitiveTypeConstraint { get; }

    internal abstract bool hasObjectTypeConstraint { get; }

    internal abstract bool isPrimitiveTypeFromConstraintTypes { get; }

    internal abstract bool isObjectTypeFromConstraintTypes { get; }

    internal abstract ConstantValue defaultValue { get; }

    internal new virtual TemplateParameterSymbol originalDefinition => this;

    private protected sealed override TypeSymbol _originalTypeSymbolDefinition => originalDefinition;

    internal NamedTypeSymbol effectiveBaseClass {
        get {
            EnsureConstraintsAreResolved();
            return GetEffectiveBaseClass(ConsList<TemplateParameterSymbol>.Empty);
        }
    }

    internal ImmutableArray<TypeWithAnnotations> constraintTypes {
        get {
            EnsureConstraintsAreResolved();
            return GetConstraintTypes(ConsList<TemplateParameterSymbol>.Empty);
        }
    }

    internal sealed override bool isObjectType {
        get {
            if (hasObjectTypeConstraint)
                return true;

            return isObjectTypeFromConstraintTypes;
        }
    }

    internal sealed override bool isPrimitiveType {
        get {
            if (hasPrimitiveTypeConstraint)
                return true;

            return isPrimitiveTypeFromConstraintTypes;
        }
    }

    internal abstract ImmutableArray<TypeWithAnnotations> GetConstraintTypes(ConsList<TemplateParameterSymbol> inProgress);

    internal abstract NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress);

    // TODO Ensure this is needed
    internal abstract TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress);

    internal abstract void EnsureConstraintsAreResolved();

    private protected static void EnsureConstraintsAreResolved(ImmutableArray<TemplateParameterSymbol> templateParameters) {
        foreach (var templateParameter in templateParameters) {
            var _ = templateParameter.GetConstraintTypes(ConsList<TemplateParameterSymbol>.Empty);
        }
    }

    internal sealed override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal sealed override ImmutableArray<Symbol> GetMembers(string name) {
        return [];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [];
    }

    internal static bool CalculateIsPrimitiveTypeFromConstraintTypes(
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        foreach (var constraintType in constraintTypes) {
            if (constraintType.type.isPrimitiveType)
                return true;
        }

        return false;
    }

    internal static bool CalculateIsObjectTypeFromConstraintTypes(
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        foreach (var constraintType in constraintTypes) {
            if (ConstraintImpliesObjectType(constraintType.type))
                return true;
        }

        return false;
    }

    internal static bool NonTypeParameterConstraintImpliesObjectType(TypeSymbol constraint) {
        if (!constraint.isObjectType) {
            return false;
        } else {
            if (constraint.typeKind == TypeKind.Error)
                return false;

            return true;
        }
    }

    internal override bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
        return Equals(other as TemplateParameterSymbol, compareKind);
    }

    internal bool Equals(TemplateParameterSymbol other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    private bool Equals(TemplateParameterSymbol other, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, other))
            return true;

        if ((object)other is null || !ReferenceEquals(other.originalDefinition, originalDefinition))
            return false;

        return other.containingSymbol.containingType.Equals(containingSymbol.containingType, compareKind);
    }

    private static bool ConstraintImpliesObjectType(TypeSymbol constraint) {
        if (constraint.typeKind == TypeKind.TemplateParameter) {
            return ((TemplateParameterSymbol)constraint).isObjectTypeFromConstraintTypes;
        }

        return NonTypeParameterConstraintImpliesObjectType(constraint);
    }

    public override int GetHashCode() {
        return Hash.Combine(containingSymbol, ordinal);
    }
}
