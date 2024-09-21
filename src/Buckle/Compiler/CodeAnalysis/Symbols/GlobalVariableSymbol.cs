
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A global variable symbol. This is a variable declared in the global scope.
/// </summary>
internal sealed class GlobalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="GlobalVariableSymbol" />.
    /// </summary>
    internal GlobalVariableSymbol(
        string name,
        TypeWithAnnotations type,
        ConstantValue constant,
        DeclarationModifiers modifiers)
        : base(name, type, constant, modifiers, Accessibility.NotApplicable) { }

    public override SymbolKind kind => SymbolKind.GlobalVariable;
}
