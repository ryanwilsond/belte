
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol with null clarification.
/// </summary>
internal sealed class TypeWithAnnotations {
    internal TypeWithAnnotations(TypeSymbol underlyingType, bool isNullable) {
        type = underlyingType;
        this.isNullable = isNullable;
    }

    internal TypeSymbol type { get; }

    internal bool isNullable { get; }

    public bool Equals(TypeWithAnnotations other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeWithAnnotations other, TypeCompareKind compareKind) {
        if (compareKind == TypeCompareKind.ConsiderEverything)
            return isNullable == other.isNullable && type == other.type;

        if (compareKind == TypeCompareKind.IgnoreNullability)
            return type == other.type;

        return false;
    }
}
