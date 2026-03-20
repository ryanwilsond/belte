using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ConstructedErrorTypeSymbol : SubstitutedErrorTypeSymbol {
    private readonly ErrorTypeSymbol _constructedFrom;

    internal ConstructedErrorTypeSymbol(
        ErrorTypeSymbol constructedFrom,
        ImmutableArray<TypeOrConstant> templateArguments)
        : base((ErrorTypeSymbol)constructedFrom.originalDefinition) {
        _constructedFrom = constructedFrom;
        this.templateArguments = templateArguments;
        templateSubstitution = new TemplateMap(
            constructedFrom.containingType,
            constructedFrom.originalDefinition.templateParameters,
            templateArguments
        );
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => _constructedFrom.templateParameters;

    public override TemplateMap templateSubstitution { get; }

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }

    // TODO replace this with actual constraints
    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override NamedTypeSymbol constructedFrom => _constructedFrom;

    internal override Symbol containingSymbol => _constructedFrom.containingSymbol;
}
