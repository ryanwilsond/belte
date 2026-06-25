using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceOverridingMethodTemplateParameterSymbol : SourceTemplateParameterSymbolBase {
    private readonly OverriddenMethodTemplateParameterMapBase _map;

    internal SourceOverridingMethodTemplateParameterSymbol(
        OverriddenMethodTemplateParameterMapBase map,
        string name,
        int ordinal,
        SyntaxReference syntaxReference)
        : base(name, ordinal, syntaxReference) {
        _map = map;
    }

    internal SourceOrdinaryMethodSymbol owner => _map.overridingMethod;

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Method;

    internal override Symbol containingSymbol => owner;

    internal override bool allowsRefLikeType {
        get {
            var typeParameter = _overriddenTemplateParameter;
            return (typeParameter is not null) && typeParameter.allowsRefLikeType;
        }
    }

    internal override bool hasNotNullConstraint => _overriddenTemplateParameter?.hasNotNullConstraint == true;

    internal override bool hasValueTypeConstraint
        => _overriddenTemplateParameter?.hasValueTypeConstraint == true;

    internal override bool hasReferenceTypeConstraint
        => _overriddenTemplateParameter?.hasReferenceTypeConstraint == true;

    internal override bool hasDefaultConstraint => _overriddenTemplateParameter?.hasDefaultConstraint == true;

    internal override bool hasConstructorConstraint => _overriddenTemplateParameter?.hasConstructorConstraint == true;

    internal override bool isValueTypeFromConstraintTypes
        => _overriddenTemplateParameter?.isValueTypeFromConstraintTypes == true;

    internal override bool isReferenceTypeFromConstraintTypes
        => _overriddenTemplateParameter?.isReferenceTypeFromConstraintTypes == true;

    internal override bool hasDefaultFromConstraintTypes
        => _overriddenTemplateParameter?.hasDefaultFromConstraintTypes == true;

    internal override bool hasConstructorFromConstraintTypes
        => _overriddenTemplateParameter?.hasConstructorFromConstraintTypes == true;

    private protected override ImmutableArray<TemplateParameterSymbol> _containerTemplateParameters
        => owner.templateParameters;

    private TemplateParameterSymbol _overriddenTemplateParameter => _map.GetOverriddenTemplateParameter(ordinal);

    private protected override TypeParameterBounds ResolveBounds(
        ConsList<TemplateParameterSymbol> inProgress,
        BelteDiagnosticQueue diagnostics) {
        var templateParameter = _overriddenTemplateParameter;

        if (templateParameter is null)
            return null;

        var map = _map.templateMap;
        var constraintTypes = map.SubstituteTypes(templateParameter.constraintTypes).SelectAsArray(t => t.type);

        return this.ResolveBounds(
            inProgress.Prepend(this),
            constraintTypes,
            true,
            declaringCompilation,
            diagnostics,
            location
        );
    }
}
