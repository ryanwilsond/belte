using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ConstructedNamedTypeSymbol : SubstitutedNamedTypeSymbol {
    // TODO This constructor should take in template constraint expressions as well
    internal ConstructedNamedTypeSymbol(
        NamedTypeSymbol constructedFrom,
        ImmutableArray<TypeOrConstant> templateArguments,
        bool isUnboundTemplateType = false)
        : base(
            constructedFrom.containingSymbol,
            new TemplateMap(
                constructedFrom.containingType,
                constructedFrom.originalDefinition.templateParameters,
                templateArguments
            ),
            constructedFrom.originalDefinition,
            constructedFrom,
            isUnboundTemplateType) {
        this.templateArguments = templateArguments;
        this.constructedFrom = constructedFrom;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override NamedTypeSymbol constructedFrom { get; }

    internal static bool TemplateParametersMatchTemplateArguments(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments) {
        var n = templateParameters.Length;

        for (var i = 0; i < n; i++) {
            if (!templateArguments[i].type.type.Equals(templateParameters[i]))
                return false;
        }

        return true;
    }
}
