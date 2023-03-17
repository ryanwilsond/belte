using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A local variable symbol. This is a variable declared anywhere except the global scope (thus being more local).
/// </summary>
internal class LocalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="LocalVariableSymbol" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="type"><see cref="BoundType" /> of the variable.</param>
    /// <param name="constant"><see cref="BoundConstant" /> of the variable.</param>
    internal LocalVariableSymbol(string name, BoundType type, BoundConstant constant)
        : base(name, type, constant) { }

    internal override SymbolKind kind => SymbolKind.LocalVariable;
}
