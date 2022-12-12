using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A function symbol.
/// </summary>
internal sealed class FunctionSymbol : Symbol {
    /// <summary>
    /// Creates a function symbol.
    /// </summary>
    /// <param name="name">Name of function</param>
    /// <param name="parameters">Parameters of function</param>
    /// <param name="typeClause">Type clause of return type</param>
    /// <param name="declaration">Declaration of function</param>
    internal FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters,
        BoundTypeClause typeClause, FunctionDeclaration declaration = null)
        : base(name) {
        this.typeClause = typeClause;
        this.parameters = parameters;
        this.declaration = declaration;
    }

    /// <summary>
    /// All parameters (see ParameterSymbol).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    /// <summary>
    /// Type clause of function return type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Declaration of function (see FunctionDeclaration).
    /// </summary>
    internal FunctionDeclaration declaration { get; }

    internal override SymbolType type => SymbolType.Function;
}
