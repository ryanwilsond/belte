using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound type, partially mutable.
/// </summary>
internal sealed class BoundType : BoundNode {
    /// <summary>
    /// Decimal type that can be null.
    /// </summary>
    internal static readonly BoundType NullableDecimal = new BoundType(TypeSymbol.Decimal, isNullable: true);

    /// <summary>
    /// Integer type that can be null.
    /// </summary>
    internal static readonly BoundType NullableInt = new BoundType(TypeSymbol.Int, isNullable: true);

    /// <summary>
    /// String type that can be null.
    /// </summary>
    internal static readonly BoundType NullableString = new BoundType(TypeSymbol.String, isNullable: true);

    /// <summary>
    /// Boolean type that can be null.
    /// </summary>
    internal static readonly BoundType NullableBool = new BoundType(TypeSymbol.Bool, isNullable: true);

    /// <summary>
    /// Any type that can be null.
    /// </summary>
    internal static readonly BoundType NullableAny = new BoundType(TypeSymbol.Any, isNullable: true);

    /// <summary>
    /// The type type, value can be a type clause, can be null.
    /// </summary>
    internal static readonly BoundType NullableType = new BoundType(TypeSymbol.Type, isNullable: true);

    /// <summary>
    /// Decimal type that cannot be null.
    /// </summary>
    internal static readonly BoundType Decimal = new BoundType(TypeSymbol.Decimal);

    /// <summary>
    /// Integer type that cannot be null.
    /// </summary>
    internal static readonly BoundType Int = new BoundType(TypeSymbol.Int);

    /// <summary>
    /// String type that cannot be null.
    /// </summary>
    internal static readonly BoundType String = new BoundType(TypeSymbol.String);

    /// <summary>
    /// Boolean type that cannot be null.
    /// </summary>
    internal static readonly BoundType Bool = new BoundType(TypeSymbol.Bool);

    /// <summary>
    /// Any type that cannot be null.
    /// </summary>
    internal static readonly BoundType Any = new BoundType(TypeSymbol.Any);

    /// <summary>
    /// The type type, value can be a <see cref="BoundType" />, cannot be null.
    /// </summary>
    internal static readonly BoundType Type = new BoundType(TypeSymbol.Type);

    /// <param name="typeSymbol">The language type, not the <see cref="Node" /> type.</param>
    /// <param name="isImplicit">If the type was assumed by the var or let keywords.</param>
    /// <param name="isConstantReference">If the type is an unchanging reference type.</param>
    /// <param name="isReference">If the type is a reference type.</param>
    /// <param name="isConstant">If the value this type is referring to is only defined once.</param>
    /// <param name="isNullable">If the value this type is referring to can be null.</param>
    /// <param name="isLiteral">If the type was assumed from a literal.</param>
    /// <param name="dimensions">Dimensions of the type, 0 if not an array.</param>
    internal BoundType(
        TypeSymbol typeSymbol, bool isImplicit = false, bool isConstantReference = false, bool isReference = false,
        bool isExplicitReference = false, bool isConstant = false, bool isNullable = false,
        bool isLiteral = false, int dimensions = 0) {
        this.typeSymbol = typeSymbol;
        this.isImplicit = isImplicit;
        this.isConstantReference = isConstantReference;
        this.isReference = isReference;
        this.isExplicitReference = isExplicitReference;
        this.isConstant = isConstant;
        this.isNullable = isNullable;
        this.isLiteral = isLiteral;
        this.dimensions = dimensions;
    }

    /// <summary>
    /// The language type, not the <see cref="Node" /> type.
    /// </summary>
    internal TypeSymbol typeSymbol { get; }

    /// <summary>
    /// If the type was assumed by the var or let keywords.
    /// </summary>
    internal bool isImplicit { get; }

    /// <summary>
    /// If the type is an unchanging reference type.
    /// </summary>
    internal bool isConstantReference { get; }

    /// <summary>
    /// If the type is a reference type.
    /// </summary>
    internal bool isReference { get; }

    /// <summary>
    /// If the type is explicitly a reference expression, versus a reference type.
    /// </summary>
    internal bool isExplicitReference { get; }

    /// <summary>
    /// If the value this type is referring to is only defined once.
    /// </summary>
    internal bool isConstant { get; }

    /// <summary>
    /// If the value this type is referring to can be null.
    /// </summary>
    internal bool isNullable { get; }

    /// <summary>
    /// If the type was assumed from a literal.
    /// </summary>
    internal bool isLiteral { get; }

    /// <summary>
    /// Dimensions of the type, 0 if not an array
    /// </summary>
    internal int dimensions { get; }

    internal override BoundNodeKind kind => BoundNodeKind.Type;

    public override string ToString() {
        var text = "";

        if (!isNullable && !isLiteral)
            text += "[NotNull]";

        if (isConstantReference && isExplicitReference)
            text += "const ";
        if (isExplicitReference)
            text += "ref ";
        if (isConstant)
            text += "const ";

        text += typeSymbol.name;

        for (int i=0; i<dimensions; i++)
            text += "[]";

        return text;
    }

    /// <summary>
    /// If the <see cref="BoundType.typeSymbol" />, <see cref="BoundType.isReference" />,
    /// and <see cref="BoundType.dimensions" /> are the same between the two types.
    /// </summary>
    /// <param name="a"><see cref="BoundType" /> to compare.</param>
    /// <param name="b"><see cref="BoundType" /> to compare.</param>
    /// <returns>If described fields match.</returns>
    internal static bool AboutEqual(BoundType a, BoundType b) {
        if (a.typeSymbol != b.typeSymbol)
            return false;
        if (a.isReference != b.isReference)
            return false;
        if (a.dimensions != b.dimensions)
            return false;

        return true;
    }

    /// <summary>
    /// If all fields are the same between the two types, they do not need to reference the same object in memory.
    /// </summary>
    /// <param name="a"><see cref="BoundType" /> to compare.</param>
    /// <param name="b"><see cref="BoundType" /> to compare.</param>
    /// <returns>If all fields match.</returns>
    internal static bool Equals(BoundType a, BoundType b) {
        // A little brute force
        if (a.typeSymbol != b.typeSymbol)
            return false;
        if (a.isImplicit != b.isImplicit)
            return false;
        if (a.isConstantReference != b.isConstantReference)
            return false;
        if (a.isReference != b.isReference)
            return false;
        if (a.isConstant != b.isConstant)
            return false;
        if (a.isNullable != b.isNullable)
            return false;
        if (a.isLiteral != b.isLiteral)
            return false;
        if (a.dimensions != b.dimensions)
            return false;

        return true;
    }

    /// <summary>
    /// Copies all data to a new <see cref="BoundType" />, not a reference.
    /// Optionally, any specific override values available in the constructor can be specified.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to copy.</param>
    /// <returns>New copy <see cref="BoundType" />.</returns>
    internal static BoundType Copy(
        BoundType type, TypeSymbol typeSymbol = null, bool? isImplicit = null, bool? isConstantReference = null,
        bool? isReference = null, bool? isExplicitReference = null, bool? isConstant = null, bool? isNullable = null,
        bool? isLiteral = null, int? dimensions = null) {
        return new BoundType(
            typeSymbol ?? type.typeSymbol,
            isImplicit ?? type.isImplicit,
            isConstantReference ?? type.isConstantReference,
            isReference ?? type.isReference,
            isExplicitReference ?? type.isExplicitReference,
            isConstant ?? type.isConstant,
            isNullable ?? type.isNullable,
            isLiteral ?? type.isLiteral,
            dimensions ?? type.dimensions
        );
    }

    /// <summary>
    /// Copies all data to a new <see cref="BoundType" />, but makes the new <see cref="BoundType" /> non nullable.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to copy.</param>
    /// <returns>Non nullable copy <see cref="BoundType" />.</returns>
    internal static BoundType NonNullable(BoundType type) => Copy(type, isNullable: false);

    /// <summary>
    /// The item <see cref="BoundType" /> if this <see cref="BoundType" /> is an array, otherwise null.
    /// </summary>
    /// <returns><see cref="BoundType" /> of item type.</returns>
    internal BoundType ChildType() {
        if (dimensions > 0)
            return Copy(this, dimensions: dimensions - 1);
        else
            return null;
    }

    /// <summary>
    /// The base item <see cref="BoundType" />, no dimensions. If this is not an array, returns this.
    /// </summary>
    /// <returns>The base item <see cref="BoundType" />.</returns>
    internal BoundType BaseType() {
        if (dimensions > 0)
            return Copy(this, dimensions: 0);
        else
            return this;
    }
}
