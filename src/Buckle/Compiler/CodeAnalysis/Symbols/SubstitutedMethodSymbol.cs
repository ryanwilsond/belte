using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal sealed class SubstitutedMethodSymbol : MethodSymbol {
    internal SubstitutedMethodSymbol(
        MethodSymbol originalDefinition,
        ImmutableArray<TypeOrConstant> templateArguments,
        TemplateMap templateSubstitution)
        : base(
            originalDefinition.name,
            originalDefinition.templateParameters,
            originalDefinition.templateConstraints,
            originalDefinition.parameters,
            originalDefinition.typeWithAnnotations,
            originalDefinition.declaration,
            originalDefinition.modifiers,
            originalDefinition.accessibility
        ) {
        originalMethodDefinition = originalDefinition;
        this.templateArguments = templateArguments;
        this.templateSubstitution = templateSubstitution;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }

    public override TemplateMap templateSubstitution { get; }

    public override MethodSymbol originalMethodDefinition { get; }
}
