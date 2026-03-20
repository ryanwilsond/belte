using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ConstructedMethodSymbol : SubstitutedMethodSymbol {
    internal ConstructedMethodSymbol(
        MethodSymbol constructedFrom,
        ImmutableArray<TypeOrConstant> templateArguments)
        : base(
            constructedFrom.containingType,
            new TemplateMap(
                constructedFrom.containingType,
                constructedFrom.originalDefinition.templateParameters,
                templateArguments
            ),
            constructedFrom.originalDefinition,
            constructedFrom) {
        this.templateArguments = templateArguments;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }
}
