using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A global variable symbol. This is a variable declared in the global scope.
/// </summary>
internal sealed class GlobalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="GlobalVariableSymbol" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="type"><see cref="BoundType" /> of the variable.</param>
    /// <param name="constant"><see cref="BoundConstant" /> of the variable.</param>
    internal GlobalVariableSymbol(string name, BoundType type, BoundConstant constant, DeclarationModifiers modifiers)
        : base(name, type, constant, modifiers) { }

    public override SymbolKind kind => SymbolKind.GlobalVariable;
}
