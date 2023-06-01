using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

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

    /// <param name="typeSymbol">The language type, not the <see cref="Syntax.SyntaxNode" /> type.</param>
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
        bool isLiteral = false, int dimensions = 0, ImmutableArray<BoundConstant>? templateArguments = null) {
        this.typeSymbol = typeSymbol;
        this.isImplicit = isImplicit;
        this.isConstantReference = isConstantReference;
        this.isReference = isReference;
        this.isExplicitReference = isExplicitReference;
        this.isConstant = isConstant;
        this.isNullable = isNullable;
        this.isLiteral = isLiteral;
        this.dimensions = dimensions;
        this.templateArguments = templateArguments ?? ImmutableArray<BoundConstant>.Empty;
    }

    /// <summary>
    /// The language type, not the <see cref="Syntax.SyntaxNode" /> type.
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

    internal ImmutableArray<BoundConstant> templateArguments { get; }

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

        for (var i = 0; i < dimensions; i++)
            text += "[]";

        return text;
    }

    /// <summary>
    /// Copies all data to a new <see cref="BoundType" />, not a reference.
    /// Optionally, any specific override values available in the constructor can be specified.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to copy.</param>
    /// <returns>New copy <see cref="BoundType" />.</returns>
    internal static BoundType CopyWith(
        BoundType type, TypeSymbol typeSymbol = null, bool? isImplicit = null, bool? isConstantReference = null,
        bool? isReference = null, bool? isExplicitReference = null, bool? isConstant = null, bool? isNullable = null,
        bool? isLiteral = null, int? dimensions = null, ImmutableArray<BoundConstant>? templateArguments = null) {
        if (type is null)
            return null;

        return new BoundType(
            typeSymbol ?? type.typeSymbol,
            isImplicit ?? type.isImplicit,
            isConstantReference ?? type.isConstantReference,
            isReference ?? type.isReference,
            isExplicitReference ?? type.isExplicitReference,
            isConstant ?? type.isConstant,
            isNullable ?? type.isNullable,
            isLiteral ?? type.isLiteral,
            dimensions ?? type.dimensions,
            templateArguments ?? type.templateArguments
        );
    }

    /// <summary>
    /// Assumes the type of a value.
    /// </summary>
    /// <param name="value">Value to assume <see cref="BoundType" /> from.</param>
    /// <returns>The assumed <see cref="BoundType" />.</returns>
    internal static BoundType Assume(object value) {
        if (value is bool)
            return new BoundType(TypeSymbol.Bool, isLiteral: true);
        if (value is int)
            return new BoundType(TypeSymbol.Int, isLiteral: true);
        if (value is string)
            return new BoundType(TypeSymbol.String, isLiteral: true);
        if (value is double)
            return new BoundType(TypeSymbol.Decimal, isLiteral: true);
        if (value is null)
            return new BoundType(null, isLiteral: true, isNullable: true);
        else
            throw new BelteInternalException($"Assume: unexpected literal '{value}' of type '{value.GetType()}'");
    }

    /// <summary>
    /// If the given type is the same as this.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to compare this to.</param>
    /// <returns>If all fields match.</returns>
    internal bool Equals(BoundType type, bool loose = false) {
        if ((loose ? (typeSymbol is object && type.typeSymbol is object) : true) && typeSymbol != type.typeSymbol)
            return false;
        if (isImplicit != type.isImplicit)
            return false;
        if (isConstantReference != type.isConstantReference)
            return false;
        if (isReference != type.isReference)
            return false;
        if (isConstant != type.isConstant)
            return false;
        if (isNullable != type.isNullable)
            return false;
        if (isLiteral != type.isLiteral)
            return false;
        if (dimensions != type.dimensions)
            return false;
        if (templateArguments.Length != type.templateArguments.Length)
            return false;

        for (int i = 0; i < templateArguments.Length; i++) {
            if (templateArguments[i].value != type.templateArguments[i].value)
                return false;
        }

        return true;
    }

    /// <summary>
    /// The item <see cref="BoundType" /> if this <see cref="BoundType" /> is an array, otherwise null.
    /// </summary>
    /// <returns><see cref="BoundType" /> of item type.</returns>
    internal BoundType ChildType() {
        if (dimensions > 0)
            return CopyWith(this, dimensions: dimensions - 1);
        else
            return null;
    }

    /// <summary>
    /// The base item <see cref="BoundType" />, no dimensions. If this is not an array, returns this.
    /// </summary>
    /// <returns>The base item <see cref="BoundType" />.</returns>
    internal BoundType BaseType() {
        if (dimensions > 0)
            return CopyWith(this, dimensions: 0);
        else
            return this;
    }
}
