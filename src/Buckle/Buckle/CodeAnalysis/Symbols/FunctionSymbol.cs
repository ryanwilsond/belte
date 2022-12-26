using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

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
    /// <param name="type"><see cref="BoundType" /> of return type.</param>
    /// <param name="declaration">Declaration of function.</param>
    internal FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters,
        BoundType type, MethodDeclarationSyntax declaration = null)
        : base(name) {
        this.type = type;
        this.parameters = parameters;
        this.declaration = declaration;
    }

    /// <summary>
    /// All parameters (see <see cref="ParameterSymbol" />).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    /// <summary>
    /// <see cref="BoundType" /> of function return type.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// Declaration of function (see <see cref="MethodDeclarationSyntax">).
    /// </summary>
    internal MethodDeclarationSyntax declaration { get; }

    internal override SymbolKind kind => SymbolKind.Function;

    /// <summary>
    /// Compares this to <paramref name="right" /> to see if the method signatures match, even if they are not the
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
                    checkParameter.type != parameter.type)
                    parametersMatch = false;
            }

            if (parametersMatch)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the signature of this without the return type or parameter names.
    /// </summary>
    /// <returns>Signature if this <see cref="FunctionSymbol" />.</returns>
    internal string SignatureAsString() {
        var signature = new StringBuilder($"{name}(");
        var isFirst = true;

        foreach (var parameter in parameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(',');

            signature.Append(parameter.type.ToString());
        }

        signature.Append(')');
        return signature.ToString();
    }
}
