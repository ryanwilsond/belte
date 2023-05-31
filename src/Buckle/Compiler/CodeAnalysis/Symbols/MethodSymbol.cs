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
        string name, ImmutableArray<ParameterSymbol> parameters, BoundType type,
        MethodDeclarationSyntax declaration = null, MethodSymbol originalDefinition = null)
        : base(name) {
        this.type = type;
        this.parameters = parameters;
        this.declaration = declaration;
        this.originalDefinition = originalDefinition;
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

    /// <summary>
    /// If this symbol is a modification of another symbol, <see cref="originalDefinition" /> is a reference
    /// to the original symbol.
    /// </summary>
    internal MethodSymbol originalDefinition { get; }

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
    /// Creates a new method symbol with different parameters, but everything else is identical.
    /// </summary>
    internal MethodSymbol UpdateParameters(ImmutableArray<ParameterSymbol> parameters) {
        return new MethodSymbol(name, parameters, type, declaration, this);
    }

    /// <summary>
    /// Gets a string representation of the method signature (declaration only, no definition).
    /// </summary>
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

    /// <summary>
    /// If the given symbol refers to this one.
    /// </summary>
    internal bool RefersTo(MethodSymbol symbol) {
        if (symbol is null)
            return false;

        return GetRootMethod() == symbol.GetRootMethod();
    }

    /// <summary>
    /// Gets the most original definition (recursively) of this method. If no explicit original definition exists,
    /// returns this.
    /// </summary>
    internal MethodSymbol GetRootMethod() {
        if (originalDefinition == null)
            return this;

        return originalDefinition.GetRootMethod();
    }
}
