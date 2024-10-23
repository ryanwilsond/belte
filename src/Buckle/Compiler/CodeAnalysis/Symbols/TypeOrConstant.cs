
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Template argument value.
/// </summary>
internal sealed class TypeOrConstant {
    internal TypeOrConstant(ConstantValue constant) {
        this.constant = constant;
        type = null;
        isConstant = true;
    }

    internal TypeOrConstant(TypeWithAnnotations type) {
        constant = null;
        isConstant = false;
        this.type = type;
    }

    internal TypeOrConstant(TypeSymbol type, bool isNullable = false)
        : this(new TypeWithAnnotations(type, isNullable)) { }

    internal bool isConstant { get; }

    internal bool isType => !isConstant;

    internal ConstantValue constant { get; }

    internal TypeWithAnnotations type { get; }

    internal bool Equals(TypeOrConstant other, TypeCompareKind compareKind) {
        if (isConstant)
            return constant?.value == other.constant?.value;
        else
            return type.Equals(other.type, compareKind);
    }

    public override int GetHashCode() {
        if (isType)
            return type.GetHashCode();

        return constant.GetHashCode();
    }
}
