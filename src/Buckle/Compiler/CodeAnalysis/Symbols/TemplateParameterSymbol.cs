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

    internal abstract ConstantValue defaultValue { get; }

    internal bool isTypeParameter => underlyingType.type.specialType == SpecialType.Type;

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

    internal abstract ImmutableArray<TypeWithAnnotations> GetConstraintTypes(ConsList<TemplateParameterSymbol> inProgress);

    internal abstract NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress);

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

    public override int GetHashCode() {
        return Hash.Combine(containingSymbol, ordinal);
    }
}
