using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound binary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundBinaryOperator {
    private BoundBinaryOperator(
        SyntaxKind kind,
        BoundBinaryOperatorKind opKind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.leftType = leftType;
        this.rightType = rightType;
        type = resultType;
    }

    private BoundBinaryOperator(
        SyntaxKind kind,
        BoundBinaryOperatorKind opKind,
        TypeSymbol operandType,
        TypeSymbol resultType)
        : this(kind, opKind, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxKind kind, BoundBinaryOperatorKind opKind, TypeSymbol type)
        : this(kind, opKind, type, type, type) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundBinaryOperator[] Operators = {
        // integer
        new BoundBinaryOperator(
            SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr, TypeSymbol.Int),
        new BoundBinaryOperator(
            SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor, TypeSymbol.Int),
        new BoundBinaryOperator(SyntaxKind.LessThanLessThanToken, BoundBinaryOperatorKind.LeftShift,
            TypeSymbol.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanToken, BoundBinaryOperatorKind.RightShift,
            TypeSymbol.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
            BoundBinaryOperatorKind.UnsignedRightShift, TypeSymbol.Int),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            TypeSymbol.Int, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, TypeSymbol.Int),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            TypeSymbol.Int),

        // boolean
        new BoundBinaryOperator(SyntaxKind.AmpersandAmpersandToken, BoundBinaryOperatorKind.ConditionalAnd,
            TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.PipePipeToken, BoundBinaryOperatorKind.ConditionalOr,
            TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd,
            TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr,
            TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor,
            TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Bool, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Bool, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            TypeSymbol.Bool),

        // string
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            TypeSymbol.String),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.String, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.String, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            TypeSymbol.String),

        // char
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Char, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Char, TypeSymbol.Bool),

        // decimal
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction,
            TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication,
            TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division,
            TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power,
            TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            TypeSymbol.Decimal, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, TypeSymbol.Decimal),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
                TypeSymbol.Decimal),

        // type
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Type, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Type, TypeSymbol.Bool),

        // any
        new BoundBinaryOperator(SyntaxKind.IsKeyword, BoundBinaryOperatorKind.Is,
            TypeSymbol.Any, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.IsntKeyword, BoundBinaryOperatorKind.Isnt,
            TypeSymbol.Any, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.AsKeyword, BoundBinaryOperatorKind.As,
            TypeSymbol.Any, null),
        // TODO Maybe make this an explicit operator that does not implicitly cast
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            TypeSymbol.Any, TypeSymbol.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            TypeSymbol.Any, TypeSymbol.Bool),

        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing, null),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundBinaryOperatorKind opKind { get; }

    /// <summary>
    /// Left side operand type.
    /// </summary>
    internal TypeSymbol leftType { get; }

    /// <summary>
    /// Right side operand type.
    /// </summary>
    internal TypeSymbol rightType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal TypeSymbol type { get; }

    internal static BoundBinaryOperator BindWithOverloading(
        SyntaxToken operatorToken,
        SyntaxKind kind,
        BoundExpression left,
        BoundExpression right,
        OverloadResolution overloadResolution,
        out OverloadResolutionResult<MethodSymbol> result) {
        var name = SyntaxFacts.GetOperatorMemberName(kind, 2);

        if (name is not null) {
            var symbols = ((left.type.typeSymbol is NamedTypeSymbol l) ? l.GetMembers(name) : [])
                .AddRange((right.type.typeSymbol is NamedTypeSymbol r && left.type.typeSymbol != right.type.typeSymbol)
                    ? r.GetMembers(name)
                    : [])
                .Where(m => m is MethodSymbol)
                .Select(m => m as MethodSymbol)
                .ToImmutableArray();

            if (symbols.Length > 0) {
                result = overloadResolution.SuppressedMethodOverloadResolution(
                    symbols,
                    [(null, left), (null, right)],
                    name,
                    operatorToken,
                    null,
                    left.type.typeSymbol == symbols[0].containingType ? left.type : right.type
                );

                if (result.succeeded || result.ambiguous)
                    return null;
            }
        }

        result = OverloadResolutionResult<MethodSymbol>.Failed();

        return Bind(kind, left.type, right.type);
    }

    /// <summary>
    /// Attempts to bind an operator with given sides.
    /// </summary>
    /// <param name="kind">Operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundBinaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundBinaryOperator Bind(SyntaxKind kind, BoundType leftType, BoundType rightType) {
        foreach (var op in Operators) {
            var leftIsCorrect = op.leftType is null
                ? true
                : Conversion.Classify(leftType, op.leftType, false).isImplicit;

            var rightIsCorrect = op.rightType is null
                ? true
                : Conversion.Classify(rightType, op.rightType, false).isImplicit;

            if (op.kind == kind && leftIsCorrect && rightIsCorrect) {
                if (op.leftType is null || op.rightType is null || op.type is null) {
                    return new BoundBinaryOperator(
                        op.kind,
                        op.opKind,
                        op.leftType ?? leftType,
                        op.rightType ?? rightType,
                        op.type ?? rightType
                    );
                } else if (leftType.isNullable || rightType.isNullable) {
                    return new BoundBinaryOperator(
                        op.kind,
                        op.opKind,
                        TypeSymbol.CopyWith(op.leftType, isNullable: true),
                        TypeSymbol.CopyWith(op.rightType, isNullable: true),
                        TypeSymbol.CopyWith(op.type, isNullable: true)
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
