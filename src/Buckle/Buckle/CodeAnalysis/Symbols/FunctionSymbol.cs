using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A function symbol.
/// </summary>
internal sealed class FunctionSymbol : Symbol {
    /// <summary>
    /// Creates a <see cref="FunctionSymbol" />.
    /// </summary>
    /// <param name="name">Name of function.</param>
    /// <param name="parameters">Parameters of function.</param>
    /// <param name="typeClause"><see cref="BoundTypeClause" /> of return type.</param>
    /// <param name="declaration">Declaration of function.</param>
    internal FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters,
        BoundTypeClause typeClause, FunctionDeclaration declaration = null)
        : base(name) {
        this.typeClause = typeClause;
        this.parameters = parameters;
        this.declaration = declaration;
    }

    /// <summary>
    /// All parameters (see <see cref="ParameterSymbol" />).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    /// <summary>
    /// <see cref="BoundTypeClause" /> of function return type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Declaration of function (see <see cref="FunctionDeclaration">).
    /// </summary>
    internal FunctionDeclaration declaration { get; }

    internal override SymbolType type => SymbolType.Function;
}
