using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound binary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundBinaryOperator {
    private BoundBinaryOperator(
        SyntaxKind kind, BoundBinaryOperatorKind opKind,
        BoundType leftType, BoundType rightType, BoundType resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.leftType = leftType;
        this.rightType = rightType;
        type = resultType;
    }

    private BoundBinaryOperator(
        SyntaxKind kind, BoundBinaryOperatorKind opKind, BoundType operandType, BoundType resultType)
        : this(kind, opKind, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxKind kind, BoundBinaryOperatorKind opKind, BoundType type)
        : this(kind, opKind, type, type, type) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundBinaryOperator[] _operators = {
        // integer
        new BoundBinaryOperator(
            SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr, BoundType.Int),
        new BoundBinaryOperator(
            SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor, BoundType.Int),
        new BoundBinaryOperator(SyntaxKind.LessThanLessThanToken, BoundBinaryOperatorKind.LeftShift,
            BoundType.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanToken, BoundBinaryOperatorKind.RightShift,
            BoundType.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
            BoundBinaryOperatorKind.UnsignedRightShift, BoundType.Int),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            BoundType.Int, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, BoundType.Int),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundType.NullableInt),

        // boolean
        new BoundBinaryOperator(SyntaxKind.AmpersandAmpersandToken, BoundBinaryOperatorKind.ConditionalAnd,
            BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.PipePipeToken, BoundBinaryOperatorKind.ConditionalOr,
            BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd,
            BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr,
            BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor,
            BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundType.Bool, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundType.Bool, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundType.NullableBool),

        // string
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            BoundType.String),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundType.String, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundType.String, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundType.NullableString),

        // decimal
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction,
            BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication,
            BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division,
            BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power,
            BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            BoundType.Decimal, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, BoundType.Decimal),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
                BoundType.NullableDecimal),

        // any
        new BoundBinaryOperator(SyntaxKind.IsKeyword, BoundBinaryOperatorKind.Is,
            BoundType.NullableAny, BoundType.Bool),
        new BoundBinaryOperator(SyntaxKind.IsntKeyword, BoundBinaryOperatorKind.Isnt,
            BoundType.NullableAny, BoundType.Bool),
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
    internal BoundType leftType { get; }

    /// <summary>
    /// Right side operand type.
    /// </summary>
    internal BoundType rightType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// Attempts to bind an operator with given sides.
    /// </summary>
    /// <param name="kind">Operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundBinaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundBinaryOperator Bind(SyntaxKind kind, BoundType leftType, BoundType rightType) {
        foreach (var op in _operators) {
            var leftIsCorrect = op.leftType is null
                ? true
                : Cast.Classify(leftType, op.leftType, false).isImplicit;

            var rightIsCorrect = op.rightType is null
                ? true
                : Cast.Classify(rightType, op.rightType, false).isImplicit;

            if (op.kind == kind && leftIsCorrect && rightIsCorrect) {
                if (op.leftType is null || op.rightType is null) {
                    return new BoundBinaryOperator(
                        op.kind,
                        op.opKind,
                        op.leftType ?? leftType,
                        op.rightType ?? rightType,
                        op.type ?? leftType
                    );
                } else if (leftType.isNullable || rightType.isNullable) {
                    return new BoundBinaryOperator(
                        op.kind,
                        op.opKind,
                        BoundType.CopyWith(op.leftType, isNullable: true),
                        BoundType.CopyWith(op.rightType, isNullable: true),
                        BoundType.CopyWith(op.type, isNullable: true)
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
