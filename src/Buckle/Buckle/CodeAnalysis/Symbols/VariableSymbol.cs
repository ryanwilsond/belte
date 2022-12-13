using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A variable symbol. This can be any type of variable.
/// </summary>
internal abstract class VariableSymbol : Symbol {
    /// <summary>
    /// Creates a <see cref="VariableSymbol" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="typeClause"><see cref="BoundTypeClause" /> of the variable.</param>
    /// <param name="constant">Constant value of the variable.</param>
    internal VariableSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name) {
        this.typeClause = typeClause;
        constantValue = typeClause.isConstant && !typeClause.isReference ? constant : null;
    }

    /// <summary>
    /// <see cref="BoundTypeClause" /> of the variable.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Constant value of the variable (can be null).
    /// </summary>
    internal BoundConstant constantValue { get; }
}
