using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A class symbol.
/// </summary>
internal sealed class ClassSymbol : NamedTypeSymbol {
    /// <summary>
    /// Creates a <see cref="ClassSymbol" /> with template parameters and child members.
    /// </summary>
    internal ClassSymbol(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        ClassDeclarationSyntax declaration,
        NamedTypeSymbol containingType = null)
        : base(templateParameters, symbols, declaration, containingType) {
    }
}
