using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A struct symbol.
/// </summary>
internal sealed class StructSymbol : NamedTypeSymbol {
    /// <summary>
    /// Creates a <see cref="StructSymbol" /> with template parameters and child members.
    /// </summary>
    internal StructSymbol(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<Symbol> symbols,
        StructDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(templateParameters, templateConstraints, symbols, declaration, modifiers, accessibility) { }
}
