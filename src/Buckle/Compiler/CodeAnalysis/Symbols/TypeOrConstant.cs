
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Template argument value.
/// </summary>
internal sealed partial class TypeOrConstant {
    private bool _isTemplateSpecializedType;

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

    internal TypeOrConstant(TypeSymbol type, bool? isNullable = null)
        : this(isNullable is null ? new TypeWithAnnotations(type) : new TypeWithAnnotations(type, isNullable.Value)) { }

    internal static TypeOrConstant CreateTemplateSpecialized(TypeSymbol type) {
        return new TypeOrConstant(type) {
            _isTemplateSpecializedType = true
        };
    }

    internal bool isConstant { get; }

    internal bool isType => !isConstant;

    internal ConstantValue constant { get; }

    internal TypeWithAnnotations type { get; }

    internal bool isTemplateSpecializedType => _isTemplateSpecializedType;

    internal bool IsSameAs(TypeOrConstant other) {
        if (isConstant != other.isConstant)
            return false;

        if (isConstant)
            return constant?.Equals(other.constant) ?? true;
        else
            return type.IsSameAs(other.type);
    }

    internal TypeOrConstant Substitute(TemplateMap templateMap) {
        if (isType)
            return type.SubstituteType(templateMap);

        if (constant is TemplateConstantValue templateConstantValue)
            return templateConstantValue.Substitute(templateMap);

        return this;
    }

    internal bool Equals(TypeOrConstant other, TypeCompareKind compareKind) {
        if (isConstant != other.isConstant)
            return false;

        if (isConstant)
            return constant?.Equals(other.constant) ?? false;
        else
            return type.Equals(other.type, compareKind);
    }

    public override int GetHashCode() {
        if (isType)
            return type.GetHashCode();

        return constant.GetHashCode();
    }
}
