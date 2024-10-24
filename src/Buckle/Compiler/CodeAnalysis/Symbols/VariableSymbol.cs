
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class DataContainerSymbol : Symbol, IDataContainerSymbol {
    private protected readonly DeclarationModifiers _declarationModifiers;

    internal DataContainerSymbol(
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

    public ITypeSymbol typeSymbol => typeWithAnnotations.type;

    internal override bool isStatic
        => (_declarationModifiers & (DeclarationModifiers.Static | DeclarationModifiers.ConstExpr)) != 0;

    internal override bool isVirtual => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isOverride => false;

    internal bool isConstantReference => (_declarationModifiers & DeclarationModifiers.ConstExpr) != 0;

    internal bool isReference => (_declarationModifiers & DeclarationModifiers.Ref) != 0;

    internal bool isConstant
        => (_declarationModifiers & (DeclarationModifiers.Const | DeclarationModifiers.ConstExpr)) != 0;

    internal bool isNullable => typeWithAnnotations.isNullable;

    internal TypeWithAnnotations typeWithAnnotations { get; }

    internal TypeSymbol type => typeWithAnnotations.type;

    /// <summary>
    /// <see cref="ConstantValue" /> of the variable (can be null).
    /// </summary>
    internal ConstantValue constantValue { get; }
}
