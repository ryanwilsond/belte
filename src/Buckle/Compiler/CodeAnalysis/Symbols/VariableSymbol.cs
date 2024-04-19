using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A variable symbol. This can be any type of variable.
/// </summary>
internal abstract class VariableSymbol : Symbol, IVariableSymbol {
    private protected readonly DeclarationModifiers _declarationModifiers;

    /// <summary>
    /// Creates a <see cref="VariableSymbol" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="type"><see cref="BoundType" /> of the variable.</param>
    /// <param name="constant"><see cref="BoundConstant" /> of the variable.</param>
    internal VariableSymbol(string name, BoundType type, BoundConstant constant, DeclarationModifiers modifiers)
        : base(name) {
        this.type = type;
        constantValue = ((type?.isConstant ?? false) || (type?.isConstantExpression ?? false))
            && (!type?.isReference ?? false) ? constant : null;
        _declarationModifiers = modifiers;
    }

    public override bool isStatic
        => (_declarationModifiers & (DeclarationModifiers.Static | DeclarationModifiers.Constexpr)) != 0;

    public ITypeSymbol typeSymbol => type.typeSymbol;

    public bool isImplicit => type.isImplicit;

    public bool isConstantReference => type.isConstantReference;

    public bool isReference => type.isReference;

    public bool isExplicitReference => type.isExplicitReference;

    public bool isConstant => type.isConstant;

    public bool isNullable => type.isNullable;

    public bool isLiteral => type.isLiteral;

    public int dimensions => type.dimensions;

    /// <summary>
    /// <see cref="BoundType" /> of the variable.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// <see cref="BoundConstant" /> of the variable (can be null).
    /// </summary>
    internal BoundConstant constantValue { get; }
}
