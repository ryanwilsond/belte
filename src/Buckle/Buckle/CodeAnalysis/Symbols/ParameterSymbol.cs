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
    internal ParameterSymbol(string name, BoundType type, int ordinal) : base(name, type, null) {
        this.ordinal = ordinal;
    }

    internal override SymbolKind kind => SymbolKind.Parameter;

    /// <summary>
    /// Ordinal of this parameter.
    /// </summary>
    internal int ordinal { get; }
}
