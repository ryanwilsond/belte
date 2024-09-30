using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SubstitutedNestedTypeSymbol : SubstitutedNamedTypeSymbol {
    internal SubstitutedNestedTypeSymbol(SubstitutedNamedTypeSymbol newContainer, NamedTypeSymbol originalDefinition)
        : base(
            newContainer,
            newContainer.templateSubstitution,
            originalDefinition,
            isUnboundTemplateType: newContainer.isUnboundTemplateType && originalDefinition.arity == 0
        ) { }

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    // TODO This should be something
    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override NamedTypeSymbol constructedFrom => this;
}
