using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Parameter symbol (used in a <see cref="FunctionSymbol" />).
/// </summary>
internal sealed class ParameterSymbol : LocalVariableSymbol {
    /// <summary>
    /// Creates a <see cref="ParameterSymbol" />.
    /// </summary>
    /// <param name="name">Name of parameter.</param>
    /// <param name="type">Full <see cref="BoundType" /> of parameter.</param>
    /// <param name="ordinal">Index of which parameter it is (zero indexed).</param>
    /// <param name="defaultValue">
    /// Optional; the default value of a parameter making arguments referencing this parameter optional
    /// in CallExpressions.
    /// </param>
    internal ParameterSymbol(string name, BoundType type, int ordinal, BoundExpression defaultValue)
        : base(name, type, null) {
        this.ordinal = ordinal;
        this.defaultValue = defaultValue;
    }

    internal override SymbolKind kind => SymbolKind.Parameter;

    /// <summary>
    /// Ordinal of this parameter.
    /// </summary>
    internal int ordinal { get; }

    /// <summary>
    /// Optional; the default value of a parameter making arguments referencing this parameter optional
    /// in CallExpressions.
    /// </summary>
    internal BoundExpression defaultValue { get; }
}
