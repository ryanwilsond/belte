using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SubstitutedNestedErrorTypeSymbol : SubstitutedErrorTypeSymbol {
    private readonly ImmutableArray<TemplateParameterSymbol> _templateParameters;

    internal SubstitutedNestedErrorTypeSymbol(NamedTypeSymbol containingSymbol, ErrorTypeSymbol originalDefinition)
        : base(originalDefinition) {
        this.containingSymbol = containingSymbol;
        templateSubstitution = containingSymbol.templateSubstitution.WithAlphaRename(
            originalDefinition,
            this,
            out _templateParameters
        );
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => _templateParameters;

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    // TODO replace this with actual constraints
    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override TemplateMap templateSubstitution { get; }

    internal override NamedTypeSymbol constructedFrom => this;

    internal override Symbol containingSymbol { get; }
}
