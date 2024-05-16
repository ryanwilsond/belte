using System.Collections.Immutable;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound type, partially mutable.
/// </summary>
internal sealed class BoundType : BoundExpression {
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
    /// Character type that can be null.
    /// </summary>
    internal static readonly BoundType NullableChar = new BoundType(TypeSymbol.Char, isNullable: true);

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
    /// Character type that cannot be null.
    /// </summary>
    internal static readonly BoundType Char = new BoundType(TypeSymbol.Char);

    /// <summary>
    /// Boolean type that cannot be null.
    /// </summary>
    internal static readonly BoundType Bool = new BoundType(TypeSymbol.Bool);

    /// <summary>
    /// Any type that cannot be null.
    /// </summary>
    internal static readonly BoundType Any = new BoundType(TypeSymbol.Any);

    /// <summary>
    /// A type representing a method group.
    /// </summary>
    internal static readonly BoundType MethodGroup = new BoundType(TypeSymbol.Func);

    /// <summary>
    /// The type type, value can be a <see cref="BoundType" />, cannot be null.
    /// </summary>
    internal static readonly BoundType Type = new BoundType(TypeSymbol.Type);

    /// <summary>
    /// The type representing no type at all, a method that doesn't return.
    /// </summary>
    // Setting it nullable so it has no attributes when displayed,
    // just an implementation detail, logically void is not nullable or non-nullable.
    internal static readonly BoundType Void = new BoundType(TypeSymbol.Void, isNullable: true);

    /// <param name="typeSymbol">The language type, not the <see cref="Syntax.SyntaxNode" /> type.</param>
    /// <param name="isImplicit">If the type was assumed by the var or let keywords.</param>
    /// <param name="isConstantReference">If the type is an unchanging reference type.</param>
    /// <param name="isReference">If the type is a reference type.</param>
    /// <param name="isExplicitReference">
    /// If the type is an explicit reference expression instead of simply referencing something else.
    /// </param>
    /// <param name="isConstant">If the value this type is referring to is only defined once.</param>
    /// <param name="isNullable">If the value this type is referring to can be null.</param>
    /// <param name="isLiteral">If the type was assumed from a literal.</param>
    /// <param name="dimensions">Dimensions of the type, 0 if not an array.</param>
    /// <param name="arity">The number of template arguments on the type.</param>
    /// <param name="isConstantExpression">If the value this type is referring is a compile-time constant.</param>
    internal BoundType(
        TypeSymbol typeSymbol,
        bool isImplicit = false,
        bool isConstantReference = false,
        bool isReference = false,
        bool isExplicitReference = false,
        bool isConstant = false,
        bool isNullable = false,
        bool isLiteral = false,
        int dimensions = 0,
        ImmutableArray<BoundTypeOrConstant>? templateArguments = null,
        int arity = 0,
        bool isConstantExpression = false,
        ImmutableArray<BoundExpression>? sizes = null) {
        this.typeSymbol = typeSymbol;
        this.isImplicit = isImplicit;
        this.isConstantReference = isConstantReference;
        this.isReference = isReference;
        this.isExplicitReference = isExplicitReference;
        this.isConstant = isConstant;
        this.isNullable = isNullable;
        this.isLiteral = isLiteral;
        this.dimensions = dimensions;
        this.templateArguments = templateArguments ?? ImmutableArray<BoundTypeOrConstant>.Empty;
        this.arity = arity;
        this.isConstantExpression = isConstantExpression;
        this.sizes = sizes ?? ImmutableArray<BoundExpression>.Empty;
    }

    internal override BoundType type => this;

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
    /// If the value this type is referring is a compile-time constant.
    /// </summary>
    /// <value></value>
    internal bool isConstantExpression { get; }

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

    /// <summary>
    /// The arguments for the class template, if any.
    /// </summary>
    /// <value></value>
    internal ImmutableArray<BoundTypeOrConstant> templateArguments { get; }

    /// <summary>
    /// The number of template arguments the type has.
    /// </summary>
    internal int arity { get; }

    /// <summary>
    /// The array size specifications (only used with <see cref="BoundObjectCreationExpression"/>).
    /// </summary>
    internal ImmutableArray<BoundExpression> sizes { get; }

    internal override BoundNodeKind kind => BoundNodeKind.Type;

    public override string ToString() {
        return DisplayText.DisplayNode(this).ToString();
    }

    /// <summary>
    /// Copies all data to a new <see cref="BoundType" />, not a reference.
    /// Optionally, any specific override values available in the constructor can be specified.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to copy.</param>
    /// <returns>New copy <see cref="BoundType" />.</returns>
    internal static BoundType CopyWith(
        BoundType type,
        TypeSymbol typeSymbol = null,
        bool? isImplicit = null,
        bool? isConstantReference = null,
        bool? isReference = null,
        bool? isExplicitReference = null,
        bool? isConstant = null,
        bool? isNullable = null,
        bool? isLiteral = null,
        int? dimensions = null,
        ImmutableArray<BoundTypeOrConstant>? templateArguments = null,
        int? arity = null,
        bool? isConstantExpression = null,
        ImmutableArray<BoundExpression>? sizes = null) {
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
            templateArguments ?? type.templateArguments,
            arity ?? type.arity,
            isConstantExpression ?? type.isConstantExpression,
            sizes ?? type.sizes
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
        if (value is char)
            return new BoundType(TypeSymbol.Char, isLiteral: true);
        if (value is double)
            return new BoundType(TypeSymbol.Decimal, isLiteral: true);
        if (value is null)
            return new BoundType(null, isLiteral: true, isNullable: true);
        else
            throw new BelteInternalException($"Assume: unexpected literal '{value}' of type '{value.GetType()}'");
    }

    /// <summary>
    /// Checks for template arguments on <param name="receiver"/> that could clarify <param name="type"/>.
    /// </summary>
    internal static BoundType Compound(BoundType receiver, BoundType type) {
        if (type.typeSymbol is TemplateTypeSymbol t && receiver.templateArguments.Length > 0) {
            var resolvedType = receiver.templateArguments[t.template.ordinal - 1].type;

            return new BoundType(
                resolvedType.typeSymbol,
                resolvedType.isImplicit && type.isImplicit,
                resolvedType.isConstantReference || type.isConstantReference,
                resolvedType.isReference || type.isReference,
                resolvedType.isExplicitReference || type.isExplicitReference,
                resolvedType.isConstant || type.isConstant,
                !(!resolvedType.isNullable || !type.isNullable),
                resolvedType.isLiteral && type.isLiteral,
                resolvedType.dimensions + type.dimensions,
                resolvedType.templateArguments,
                resolvedType.arity,
                resolvedType.isConstantExpression || type.isConstantExpression,
                type.sizes.Length > 0 ? type.sizes : resolvedType.sizes
            );
        } else {
            return type;
        }
    }

    /// <summary>
    /// If the given type is the same as this.
    /// </summary>
    /// <param name="type"><see cref="BoundType" /> to compare this to.</param>
    /// <returns>If all fields match.</returns>
    internal bool Equals(BoundType type, bool loose = false) {
        if ((!loose || (typeSymbol is not null && type?.typeSymbol is not null)) && typeSymbol != type?.typeSymbol)
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
        if (arity != type.arity)
            return false;
        if (isConstantExpression != type.isConstantExpression)
            return false;
        if (sizes.Length != type.sizes.Length)
            return false;

        for (var i = 0; i < templateArguments.Length; i++) {
            if (!templateArguments[i].Equals(type.templateArguments[i]))
                return false;
        }

        for (var i = 0; i < sizes.Length; i++) {
            if (sizes[i] != type.sizes[i])
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
        else if (typeSymbol == TypeSymbol.Any)
            return this;
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
