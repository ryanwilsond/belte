using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal sealed class ConstructedMethodSymbol : MethodSymbol {
    internal ConstructedMethodSymbol(
        string name,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<ParameterSymbol> parameters,
        TypeWithAnnotations returnType,
        BaseMethodDeclarationSyntax declaration,
        MethodSymbol originalDefinition,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(
            name,
            templateParameters,
            templateConstraints,
            parameters,
            returnType,
            declaration,
            modifiers,
            accessibility
        ) {
        originalMethodDefinition = originalDefinition;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override TemplateMap templateSubstitution => null;

    public override MethodSymbol originalMethodDefinition { get; }

    /// <summary>
    /// Creates a new method symbol with different parameters, but everything else is identical.
    /// </summary>
    internal MethodSymbol UpdateParameters(ImmutableArray<ParameterSymbol> parameters) {
        return new ConstructedMethodSymbol(
            name,
            templateParameters,
            templateConstraints,
            parameters,
            typeWithAnnotations,
            declaration,
            this,
            modifiers,
            accessibility
        );
    }
}
