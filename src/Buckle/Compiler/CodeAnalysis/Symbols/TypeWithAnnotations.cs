
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol with null clarification.
/// </summary>
internal sealed class TypeWithAnnotations {
    internal TypeWithAnnotations(TypeSymbol underlyingType, bool isNullable) {
        type = underlyingType;
        this.isNullable = isNullable;
    }

    internal TypeWithAnnotations(TypeSymbol underlyingType) {
        type = underlyingType;
        isNullable = type.IsNullableType();
    }

    internal TypeSymbol type { get; }

    internal bool isNullable { get; }

    internal bool IsSameAs(TypeWithAnnotations other) {
        return ReferenceEquals(type, other.type) && isNullable == other.isNullable;
    }

    internal TypeOrConstant SubstituteType(TemplateMap templateMap) {
        var typeSymbol = type;
        var newType = templateMap.SubstituteType(typeSymbol).type;

        if (typeSymbol.typeKind != TypeKind.TemplateParameter) {
            if (typeSymbol.Equals(newType.type, TypeCompareKind.ConsiderEverything))
                return new TypeOrConstant(this);
            else if (typeSymbol.IsNullableType() && isNullable)
                return new TypeOrConstant(newType);

            return new TypeOrConstant(new TypeWithAnnotations(newType.type, isNullable));
        }

        if (newType.type.Equals(typeSymbol))
            return new TypeOrConstant(this);
        else if (type.Equals(typeSymbol))
            return new TypeOrConstant(newType);

        return new TypeOrConstant(new TypeWithAnnotations(newType.type, isNullable || newType.isNullable));
    }

    public bool Equals(TypeWithAnnotations other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeWithAnnotations other, TypeCompareKind compareKind) {
        if (IsSameAs(other))
            return true;

        if (type is null) {
            if (other.type is not null)
                return false;
        } else if (other.type is null || !type.Equals(other.type, compareKind)) {
            return false;
        }

        if ((compareKind & TypeCompareKind.IgnoreNullability) == 0)
            return isNullable == other.isNullable;

        return true;
    }

    public override int GetHashCode() {
        if (type is null)
            return 0;

        return type.GetHashCode();
    }
}
