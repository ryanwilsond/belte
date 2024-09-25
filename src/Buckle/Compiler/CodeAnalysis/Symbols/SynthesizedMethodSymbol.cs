using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal sealed class SynthesizedMethodSymbol : MethodSymbol {
    internal SynthesizedMethodSymbol(
        string name,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<ParameterSymbol> parameters,
        TypeWithAnnotations returnType)
        : base(
            name,
            templateParameters,
            templateConstraints,
            parameters,
            returnType,
            null,
            DeclarationModifiers.None,
            Accessibility.NotApplicable
        ) { }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override TemplateMap templateSubstitution => null;

    internal override MethodSymbol originalMethodDefinition => this;
}
