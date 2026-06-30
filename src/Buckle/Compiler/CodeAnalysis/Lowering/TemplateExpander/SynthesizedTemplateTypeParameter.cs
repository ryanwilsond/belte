using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateTypeParameter : WrappedTemplateParameterSymbol {
    private readonly Symbol _containingSymbol;

    internal SynthesizedTemplateTypeParameter(
        Symbol containingSymbol,
        TemplateParameterSymbol originalParameter,
        int ordinal)
        : base(originalParameter) {
        _containingSymbol = containingSymbol;
        this.ordinal = ordinal;
    }

    internal override Symbol containingSymbol => _containingSymbol;

    internal override int ordinal { get; }

    internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(ConsList<TemplateParameterSymbol> inProgress) {
        return underlyingTemplateParameter.GetConstraintTypes(inProgress);
    }

    internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        return underlyingTemplateParameter.GetDeducedBaseType(inProgress);
    }

    internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        return underlyingTemplateParameter.GetEffectiveBaseClass(inProgress);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetInterfaces(ConsList<TemplateParameterSymbol> inProgress) {
        return underlyingTemplateParameter.GetInterfaces(inProgress);
    }
}
