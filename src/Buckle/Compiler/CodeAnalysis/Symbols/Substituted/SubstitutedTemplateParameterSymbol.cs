using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class SubstitutedTemplateParameterSymbol : WrappedTemplateParameterSymbol {
    private readonly TemplateMap _templateMap;

    internal SubstitutedTemplateParameterSymbol(
        Symbol newContainer,
        TemplateMap templateMap,
        TemplateParameterSymbol substitutedFrom,
        int ordinal) : base(substitutedFrom) {
        containingSymbol = newContainer;
        this.ordinal = ordinal;
        _templateMap = templateMap;
    }

    internal override Symbol containingSymbol { get; }

    internal override int ordinal { get; }

    internal override TemplateParameterSymbol originalDefinition
        => containingSymbol.originalDefinition != underlyingTemplateParameter.containingSymbol.originalDefinition
            ? this
            : underlyingTemplateParameter.originalDefinition;

    internal override Compilation declaringCompilation => containingSymbol.declaringCompilation;

    internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        return _templateMap.SubstituteNamedType(underlyingTemplateParameter.GetEffectiveBaseClass(inProgress));
    }

    internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        return _templateMap.SubstituteType(underlyingTemplateParameter.GetDeducedBaseType(inProgress)).type.type;
    }

    internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(ConsList<TemplateParameterSymbol> inProgress) {
        var builder = ArrayBuilder<TypeWithAnnotations>.GetInstance();
        _templateMap.SubstituteConstraintTypesDistinctWithoutModifiers(
            underlyingTemplateParameter.GetConstraintTypes(inProgress),
            builder
        );

        // TODO May have to rearrange constraints to properly honor nullability here

        return builder.ToImmutable();
    }
}
