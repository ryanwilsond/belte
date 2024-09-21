
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol with null clarification.
/// </summary>
internal sealed class TypeWithAnnotations {
    /// <summary>
    /// Decimal type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableDecimal = new TypeWithAnnotations(TypeSymbol.Decimal, true);

    /// <summary>
    /// Integer type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableInt = new TypeWithAnnotations(TypeSymbol.Int, true);

    /// <summary>
    /// String type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableString = new TypeWithAnnotations(TypeSymbol.String, true);

    /// <summary>
    /// Character type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableChar = new TypeWithAnnotations(TypeSymbol.Char, true);

    /// <summary>
    /// Boolean type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableBool = new TypeWithAnnotations(TypeSymbol.Bool, true);

    /// <summary>
    /// Any type that can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableAny = new TypeWithAnnotations(TypeSymbol.Any, true);

    /// <summary>
    /// The type type, value can be a type clause, can be null.
    /// </summary>
    internal static readonly TypeWithAnnotations NullableType = new TypeWithAnnotations(TypeSymbol.Type, true);

    /// <summary>
    /// Decimal type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Decimal = new TypeWithAnnotations(TypeSymbol.Decimal, false);

    /// <summary>
    /// Integer type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Int = new TypeWithAnnotations(TypeSymbol.Int, false);

    /// <summary>
    /// String type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations String = new TypeWithAnnotations(TypeSymbol.String, false);

    /// <summary>
    /// Character type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Char = new TypeWithAnnotations(TypeSymbol.Char, false);

    /// <summary>
    /// Boolean type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Bool = new TypeWithAnnotations(TypeSymbol.Bool, false);

    /// <summary>
    /// Any type that cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Any = new TypeWithAnnotations(TypeSymbol.Any, false);

    /// <summary>
    /// The type type, value can be a <see cref="TypeWithAnnotations" />, cannot be null.
    /// </summary>
    internal static readonly TypeWithAnnotations Type = new TypeWithAnnotations(TypeSymbol.Type, false);

    internal TypeWithAnnotations(TypeSymbol underlyingType, bool isNullable) {
        this.underlyingType = underlyingType;
        this.isNullable = isNullable;
    }

    internal TypeSymbol underlyingType { get; }

    internal bool isNullable { get; }

    public bool Equals(TypeWithAnnotations other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeWithAnnotations other, TypeCompareKind compareKind) {
        if (compareKind == TypeCompareKind.ConsiderEverything)
            return isNullable == other.isNullable && underlyingType == other.underlyingType;

        if (compareKind == TypeCompareKind.IgnoreNullability)
            return underlyingType == other.underlyingType;

        return false;
    }
}
