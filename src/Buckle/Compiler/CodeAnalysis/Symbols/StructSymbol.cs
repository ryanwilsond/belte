using System.Collections.Immutable;
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
        ImmutableArray<Symbol> symbols,
        StructDeclarationSyntax declaration,
        DeclarationModifiers modifiers = DeclarationModifiers.None)
        : base(templateParameters, symbols, declaration, modifiers) { }
}
