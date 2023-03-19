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
    /// <param name="type"><see cref="BoundType" /> of the variable.</param>
    /// <param name="constant"><see cref="BoundConstant" /> of the variable.</param>
    internal VariableSymbol(string name, BoundType type, BoundConstant constant) : base(name) {
        this.type = type;
        constantValue = type.isConstant && !type.isReference ? constant : null;
    }

    /// <summary>
    /// <see cref="BoundType" /> of the variable.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// <see cref="BoundConstant" /> of the variable (can be null).
    /// </summary>
    internal BoundConstant constantValue { get; }
}
