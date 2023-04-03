using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal sealed class MethodSymbol : Symbol, IMethodSymbol {
    /// <summary>
    /// Creates a <see cref="MethodSymbol" />.
    /// </summary>
    /// <param name="name">Name of method.</param>
    /// <param name="parameters">Parameters of method.</param>
    /// <param name="type"><see cref="BoundType" /> of return type.</param>
    /// <param name="declaration">Declaration of method.</param>
    internal MethodSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters,
        BoundType type, MethodDeclarationSyntax declaration = null)
        : base(name) {
        this.type = type;
        this.parameters = parameters;
        this.declaration = declaration;
    }

    public override SymbolKind kind => SymbolKind.Method;

    /// <summary>
    /// All parameters (see <see cref="ParameterSymbol" />).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    /// <summary>
    /// <see cref="BoundType" /> of method return type.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// Declaration of method (see <see cref="MethodDeclarationSyntax">).
    /// </summary>
    internal MethodDeclarationSyntax declaration { get; }

    public string SignatureNoReturnNoParameterNames() {
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

    /// <summary>
    /// Compares this to <paramref name="right" /> to see if the method signatures match, even if they are not the
    /// same reference. This is effectively a value compare.
    /// NOTE: This does not look at bodies, and is only comparing the signature.
    /// </summary>
    /// <param name="right">Method to compare this to.</param>
    /// <returns>If the method signatures match completely.</returns>
    internal bool MethodMatches(MethodSymbol right) {
        if (name == right.name && parameters.Length == right.parameters.Length) {
            var parametersMatch = true;

            for (int i = 0; i < parameters.Length; i++) {
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

    internal string Signature() {
        var signature = new StringBuilder($"{type} {name}(");
        var isFirst = true;

        foreach (var parameter in parameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(',');

            signature.Append($"{parameter.type} {parameter.name}");
        }

        signature.Append(')');

        return signature.ToString();
    }
}
