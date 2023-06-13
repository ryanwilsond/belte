using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

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
        ImmutableArray<BoundStatement> defaultFieldAssignments,
        ClassDeclarationSyntax declaration)
        : base(templateParameters, symbols, declaration) {
        this.defaultFieldAssignments = defaultFieldAssignments;
    }

    /// <summary>
    /// Statements that assigns fields with specified initializers. Used in constructors.
    /// </summary>
    internal ImmutableArray<BoundStatement> defaultFieldAssignments { get; }
}
