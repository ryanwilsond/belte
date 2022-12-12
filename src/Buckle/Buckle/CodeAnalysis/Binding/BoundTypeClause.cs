using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound type clause, partially mutable.
/// </summary>
internal sealed class BoundTypeClause : BoundNode {
    /// <summary>
    /// Decimal type that can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableDecimal = new BoundTypeClause(TypeSymbol.Decimal, isNullable: true);

    /// <summary>
    /// Integer type that can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableInt = new BoundTypeClause(TypeSymbol.Int, isNullable: true);

    /// <summary>
    /// String type that can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableString = new BoundTypeClause(TypeSymbol.String, isNullable: true);

    /// <summary>
    /// Boolean type that can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableBool = new BoundTypeClause(TypeSymbol.Bool, isNullable: true);

    /// <summary>
    /// Any type that can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableAny = new BoundTypeClause(TypeSymbol.Any, isNullable: true);

    /// <summary>
    /// The type type, value can be a type clause, can be null.
    /// </summary>
    internal static readonly BoundTypeClause NullableType = new BoundTypeClause(TypeSymbol.Type, isNullable: true);

    /// <summary>
    /// Decimal type that cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause Decimal = new BoundTypeClause(TypeSymbol.Decimal);

    /// <summary>
    /// Integer type that cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause Int = new BoundTypeClause(TypeSymbol.Int);

    /// <summary>
    /// String type that cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause String = new BoundTypeClause(TypeSymbol.String);

    /// <summary>
    /// Boolean type that cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause Bool = new BoundTypeClause(TypeSymbol.Bool);

    /// <summary>
    /// Any type that cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause Any = new BoundTypeClause(TypeSymbol.Any);

    /// <summary>
    /// The type type, value can be a type clause, cannot be null.
    /// </summary>
    internal static readonly BoundTypeClause Type = new BoundTypeClause(TypeSymbol.Type);

    /// <param name="lType">The language type, not the node type</param>
    /// <param name="isImplicit">If the type was assumed by the var or let keywords</param>
    /// <param name="isConstantReference">If the type is an unchanging reference type</param>
    /// <param name="isReference">If the type is a reference type</param>
    /// <param name="isConstant">If the value this type is referring to is only defined once</param>
    /// <param name="isNullable">If the value this type is referring to can be null</param>
    /// <param name="isLiteral">If the type was assumed from a literal</param>
    /// <param name="dimensions">Dimensions of the type, 0 if not an array</param>
    internal BoundTypeClause(
        TypeSymbol lType, bool isImplicit = false, bool isConstantReference = false, bool isReference = false,
        bool isConstant = false, bool isNullable = false, bool isLiteral = false, int dimensions = 0) {
        this.lType = lType;
        this.isImplicit = isImplicit;
        this.isConstantReference = isConstantReference;
        this.isReference = isReference;
        this.isConstant = isConstant;
        this.isNullable = isNullable;
        this.isLiteral = isLiteral;
        this.dimensions = dimensions;
    }

    /// <summary>
    /// The language type, not the node type.
    /// </summary>
    internal TypeSymbol lType { get; }

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
    /// If the value this type is referring to is only defined once.
    /// </summary>
    internal bool isConstant { get; }

    // ! Use NonNullable and Nullable methods whenever possible
    // Only nullable because making it immutable is more convoluted then allowing a couple exceptions
    /// <summary>
    /// If the value this type is referring to can be null.
    /// </summary>
    internal bool isNullable { get; set; }

    /// <summary>
    /// If the type was assumed from a literal.
    /// </summary>
    internal bool isLiteral { get; }

    /// <summary>
    /// Dimensions of the type, 0 if not an array
    /// </summary>
    internal int dimensions { get; }

    internal override BoundNodeType type => BoundNodeType.TypeClause;

    public override string ToString() {
        var text = "";

        if (!isNullable && !isLiteral)
            text += "[NotNull]";

        if (isConstantReference)
            text += "const ";
        if (isReference)
            text += "ref ";
        if (isConstant)
            text += "const ";

        text += lType.name;

        for (int i=0; i<dimensions; i++)
            text += "[]";

        return text;
    }

    /// <summary>
    /// If the lType, isReference, and dimensions are the same between the two types.
    /// </summary>
    /// <param name="a">Type to compare</param>
    /// <param name="b">Type to compare</param>
    /// <returns>If described fields match</returns>
    internal static bool AboutEqual(BoundTypeClause a, BoundTypeClause b) {
        if (a.lType != b.lType)
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
    /// <param name="a">Type to compare</param>
    /// <param name="b">Type to compare</param>
    /// <returns>If all fields match</returns>
    internal static bool Equals(BoundTypeClause a, BoundTypeClause b) {
        // A little brute force
        if (a.lType != b.lType)
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
    /// Copy all data to a new type clause, not a reference.
    /// </summary>
    /// <param name="typeClause">Type to copy</param>
    /// <returns>New copy type</returns>
    internal static BoundTypeClause Copy(BoundTypeClause typeClause) {
        return new BoundTypeClause(
            typeClause.lType, typeClause.isImplicit, typeClause.isConstantReference, typeClause.isReference,
            typeClause.isConstant, typeClause.isNullable, typeClause.isLiteral, typeClause.dimensions);
    }

    /// <summary>
    /// Copy all data to a new type clause, but make the new type clause non nullable.
    /// </summary>
    /// <param name="typeClause">Type to copy</param>
    /// <returns>Non nullable copy type</returns>
    internal static BoundTypeClause NonNullable(BoundTypeClause typeClause) {
        return new BoundTypeClause(
            typeClause.lType, typeClause.isImplicit, typeClause.isConstantReference, typeClause.isReference,
            typeClause.isConstant, false, typeClause.isLiteral, typeClause.dimensions);
    }

    /// <summary>
    /// Copy all data to a new type clause, but make the new type clause nullable.
    /// </summary>
    /// <param name="typeClause">Type to copy</param>
    /// <returns>Nullable copy type</returns>
    internal static BoundTypeClause Nullable(BoundTypeClause typeClause) {
        return new BoundTypeClause(
            typeClause.lType, typeClause.isImplicit, typeClause.isConstantReference, typeClause.isReference,
            typeClause.isConstant, true, typeClause.isLiteral, typeClause.dimensions);
    }

    /// <summary>
    /// The item type if this type is an array, otherwise null.
    /// </summary>
    /// <returns>Bound type clause of item type</returns>
    internal BoundTypeClause ChildType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, dimensions - 1);
        else
            return null;
    }

    /// <summary>
    /// The base item type, no dimensions. If this is not an array, returns this.
    /// </summary>
    /// <returns>The base item type</returns>
    internal BoundTypeClause BaseType() {
        if (dimensions > 0)
            return new BoundTypeClause(
                lType, isImplicit, isConstantReference, isReference, isConstant, isNullable, isLiteral, 0);
        else
            return this;
    }
}
