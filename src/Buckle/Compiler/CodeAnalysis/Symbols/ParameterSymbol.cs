using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Parameter symbol (used in a <see cref="MethodSymbol" />).
/// </summary>
internal class ParameterSymbol : LocalSymbol {
    /// <summary>
    /// Creates a <see cref="ParameterSymbol" />.
    /// </summary>
    internal ParameterSymbol(
        string name,
        TypeWithAnnotations type,
        int ordinal,
        BoundExpression defaultValue,
        DeclarationModifiers modifiers = DeclarationModifiers.None,
        bool isTemplate = false)
        : base(name, type, null, modifiers) {
        this.ordinal = ordinal;
        this.defaultValue = defaultValue;
        this.isTemplate = isTemplate;
    }

    public override SymbolKind kind => SymbolKind.Parameter;

    internal override bool isStatic => base.isStatic || isTemplate;

    /// <summary>
    /// If the parameter is apart of a template parameter list.
    /// </summary>
    internal bool isTemplate { get; }

    /// <summary>
    /// Ordinal of this parameter.
    /// </summary>
    internal int ordinal { get; }

    /// <summary>
    /// Optional; the default value of a parameter making arguments referencing this parameter optional
    /// in CallExpressions.
    /// </summary>
    internal BoundExpression defaultValue { get; }

    internal static ParameterSymbol CreateWithNewName(ParameterSymbol old, string name) {
        return new ParameterSymbol(
            name,
            old.typeWithAnnotations,
            old.ordinal,
            old.defaultValue,
            old._declarationModifiers,
            old.isTemplate
        );
    }
}
