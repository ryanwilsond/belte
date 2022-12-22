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
    /// <param name="typeClause"><see cref="BoundTypeClause" /> of the variable.</param>
    /// <param name="constant"><see cref="ConstantValue" /> of the variable.</param>
    internal LocalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }

    internal override SymbolType type => SymbolType.LocalVariable;
}
