using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A variable symbol. This can be any type of variable.
/// </summary>
internal abstract class VariableSymbol : Symbol {
    /// <summary>
    /// Creates a variable symbol.
    /// </summary>
    /// <param name="name">Name of the variable</param>
    /// <param name="typeClause">Type clause of the variable</param>
    /// <param name="constant">Constant value of the variable</param>
    internal VariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name) {
        this.typeClause = typeClause;
        constantValue = typeClause.isConstant && !typeClause.isReference ? constant : null;
    }

    /// <summary>
    /// Type clause of the variable.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Constant value of the variable (can be null).
    /// </summary>
    internal BoundConstant constantValue { get; }
}
