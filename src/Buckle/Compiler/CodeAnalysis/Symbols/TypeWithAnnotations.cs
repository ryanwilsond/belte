
using Buckle.CodeAnalysis.Display;

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

    internal TypeKind typeKind => type.typeKind;

    internal bool IsSameAs(TypeWithAnnotations other) {
        return ReferenceEquals(type, other.type) && isNullable == other.isNullable;
    }

    internal bool IsNullableType() {
        return type.IsNullableType();
    }

    internal bool IsVoidType() {
        return type.IsVoidType();
    }

    internal TypeOrConstant SubstituteType(TemplateMap templateMap) {
        var typeSymbol = type;
        var newType = templateMap.SubstituteType(typeSymbol).type;

        if (!typeSymbol.IsTemplateParameter()) {
            if (typeSymbol.Equals(newType.type, TypeCompareKind.ConsiderEverything))
                return new TypeOrConstant(this);
            else if (typeSymbol.IsNullableType() && isNullable)
                return new TypeOrConstant(newType);

            return new TypeOrConstant(newType.type, isNullable);
        }

        if ((object)newType == (TemplateParameterSymbol)typeSymbol)
            return new TypeOrConstant(this);
        else if ((object)this == (TemplateParameterSymbol)typeSymbol)
            return new TypeOrConstant(newType);

        return new TypeOrConstant(newType.type, isNullable || newType.isNullable);
    }

    public string ToDisplayString(SymbolDisplayFormat format = null) {
        var emittedType = type.ToDisplayString(format);

        if (isNullable)
            return string.Concat(emittedType, "?");

        return emittedType;
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
