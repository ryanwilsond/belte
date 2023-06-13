using System.Collections.Immutable;
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
        ImmutableArray<(FieldSymbol, VariableDeclarationStatementSyntax)> defaultFieldAssignments,
        ClassDeclarationSyntax declaration)
        : base(templateParameters, symbols, declaration) {
        this.defaultFieldAssignments = defaultFieldAssignments;
    }

    /// <summary>
    /// Statements that assigns fields with specified initializers. Used in constructors.
    /// </summary>
    internal ImmutableArray<(FieldSymbol, VariableDeclarationStatementSyntax)> defaultFieldAssignments {
        get; private set;
    }

    internal void UpdateInternals(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<(FieldSymbol, VariableDeclarationStatementSyntax)> defaultFieldAssignments) {
        UpdateInternals(templateParameters, symbols);
        this.defaultFieldAssignments = defaultFieldAssignments;
    }
}
