
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
    /// <param name="type"><see cref="TypeSymbol" /> of the variable.</param>
    /// <param name="constant"><see cref="ConstantValue" /> of the variable.</param>
    internal VariableSymbol(
        string name,
        TypeWithAnnotations type,
        ConstantValue constant,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(name, accessibility) {
        typeWithAnnotations = type;
        constantValue = constant;
        _declarationModifiers = modifiers;
    }

    public override bool isStatic
        => (_declarationModifiers & (DeclarationModifiers.Static | DeclarationModifiers.ConstExpr)) != 0;

    public override bool isVirtual => false;

    public override bool isAbstract => false;

    public override bool isSealed => false;

    public override bool isOverride => false;

    public ITypeSymbol typeSymbol => typeWithAnnotations.underlyingType;

    internal bool isConstantReference => (_declarationModifiers & DeclarationModifiers.ConstExpr) != 0;

    internal bool isReference => (_declarationModifiers & DeclarationModifiers.Ref) != 0;

    internal bool isConstant
        => (_declarationModifiers & (DeclarationModifiers.Const | DeclarationModifiers.ConstExpr)) != 0;

    internal bool isNullable => (_declarationModifiers & DeclarationModifiers.NonNullable) == 0;

    internal TypeWithAnnotations typeWithAnnotations { get; }

    internal TypeSymbol type => typeWithAnnotations.underlyingType;

    /// <summary>
    /// <see cref="ConstantValue" /> of the variable (can be null).
    /// </summary>
    internal ConstantValue constantValue { get; }
}
