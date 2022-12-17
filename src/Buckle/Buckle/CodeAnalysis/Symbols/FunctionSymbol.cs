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

    /// <summary>
    /// Compares this to <paramref name="right" /> to see if the method signatures match, even if they aren't the
    /// same reference. This is effectively a value compare.
    /// NOTE: This does not look at bodies, and is only comparing the signature.
    /// </summary>
    /// <param name="right">Method to compare this to.</param>
    /// <returns>If the method signatures match completely.</returns>
    internal bool MethodMatches(FunctionSymbol right) {
        if (name == right.name && parameters.Length == right.parameters.Length) {
            var parametersMatch = true;

            for (int i=0; i<parameters.Length; i++) {
                var checkParameter = parameters[i];
                var parameter = right.parameters[i];

                // The Replace call allows rewritten nested functions that prefix parameter names with '$'
                if (checkParameter.name != parameter.name.Replace("$", "") ||
                    checkParameter.typeClause != parameter.typeClause)
                    parametersMatch = false;
            }

            if (parametersMatch)
                return true;
        }

        return false;
    }
}
