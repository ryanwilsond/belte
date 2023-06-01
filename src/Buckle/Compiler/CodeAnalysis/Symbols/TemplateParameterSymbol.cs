using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a type template parameter, which is a constant evaluated at type construction time.
/// </summary>
internal sealed class TemplateParameterSymbol : ParameterSymbol {
    /// <summary>
    /// Create a new <see cref="TemplateParameterSymbol" />.
    /// </summary>
    internal TemplateParameterSymbol(string name, BoundType type, int ordinal, BoundExpression defaultValue)
        : base(name, type, ordinal, defaultValue) { }

    public override SymbolKind kind => SymbolKind.TemplateParameter;
}
