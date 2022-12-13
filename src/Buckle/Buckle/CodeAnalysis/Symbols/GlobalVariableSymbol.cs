using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A global variable symbol. This is a variable declared in the global scope.
/// </summary>
internal sealed class GlobalVariableSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="GlobalVariable" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="typeClause"><see cref="BoundTypeClause" /> of the variable.</param>
    /// <param name="constant">Constant value of the variable.</param>
    internal GlobalVariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }

    /// <summary>
    /// Type of symbol (see <see cref="SymbolType" />).
    /// </summary>
    internal override SymbolType type => SymbolType.GlobalVariable;
}
