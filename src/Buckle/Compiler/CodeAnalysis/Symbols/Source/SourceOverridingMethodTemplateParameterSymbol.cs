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

    // TODO Do we need this?
    // internal override bool hasConstructorConstraint

    internal override bool hasNotNullConstraint {
        get {
            return _overriddenTemplateParameter?.hasNotNullConstraint == true;
        }
    }

    internal override bool allowsRefLikeType {
        get {
            var typeParameter = _overriddenTemplateParameter;
            return (typeParameter is not null) && typeParameter.allowsRefLikeType;
        }
    }

    internal override bool hasPrimitiveTypeConstraint
        => _overriddenTemplateParameter?.hasPrimitiveTypeConstraint == true;

    internal override bool hasObjectTypeConstraint => _overriddenTemplateParameter?.hasObjectTypeConstraint == true;

    internal override bool isPrimitiveTypeFromConstraintTypes
        => _overriddenTemplateParameter?.isPrimitiveTypeFromConstraintTypes == true;

    internal override bool isObjectTypeFromConstraintTypes
        => _overriddenTemplateParameter?.isObjectTypeFromConstraintTypes == true;

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
