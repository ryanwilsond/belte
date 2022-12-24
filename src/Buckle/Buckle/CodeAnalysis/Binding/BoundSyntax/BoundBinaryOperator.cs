using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound binary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundBinaryOperator {
    private BoundBinaryOperator(
        SyntaxKind kind, BoundBinaryOperatorKind opKind,
        BoundTypeClause leftType, BoundTypeClause rightType, BoundTypeClause resultType) {
        this.kind = kind;
        this.opType = opKind;
        this.leftType = leftType;
        this.rightType = rightType;
        typeClause = resultType;
    }

    private BoundBinaryOperator(
        SyntaxKind kind, BoundBinaryOperatorKind opKind, BoundTypeClause operandType, BoundTypeClause resultType)
        : this(kind, opKind, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxKind kind, BoundBinaryOperatorKind opKind, BoundTypeClause typeClause)
        : this(kind, opKind, typeClause, typeClause, typeClause) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundBinaryOperator[] _operators = {
        // integer
        new BoundBinaryOperator(
            SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxKind.LessThanLessThanToken, BoundBinaryOperatorKind.LeftShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanToken, BoundBinaryOperatorKind.RightShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
            BoundBinaryOperatorKind.UnsignedRightShift, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundTypeClause.NullableInt),

        // boolean
        new BoundBinaryOperator(SyntaxKind.AmpersandAmpersandToken, BoundBinaryOperatorKind.ConditionalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.PipePipeToken, BoundBinaryOperatorKind.ConditionalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.AmpersandToken, BoundBinaryOperatorKind.LogicalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.PipeToken, BoundBinaryOperatorKind.LogicalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.CaretToken, BoundBinaryOperatorKind.LogicalXor,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundTypeClause.NullableBool),

        // string
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            BoundTypeClause.String),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
            BoundTypeClause.NullableString),

        // decimal
        new BoundBinaryOperator(SyntaxKind.PlusToken, BoundBinaryOperatorKind.Addition,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.MinusToken, BoundBinaryOperatorKind.Subtraction,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskToken, BoundBinaryOperatorKind.Multiplication,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.SlashToken, BoundBinaryOperatorKind.Division,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.AsteriskAsteriskToken, BoundBinaryOperatorKind.Power,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.EqualsEqualsToken, BoundBinaryOperatorKind.EqualityEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.ExclamationEqualsToken, BoundBinaryOperatorKind.EqualityNotEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanToken, BoundBinaryOperatorKind.LessThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanToken, BoundBinaryOperatorKind.GreaterThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.LessThanEqualsToken, BoundBinaryOperatorKind.LessOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.GreaterThanEqualsToken, BoundBinaryOperatorKind.GreatOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.PercentToken, BoundBinaryOperatorKind.Modulo, BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxKind.QuestionQuestionToken, BoundBinaryOperatorKind.NullCoalescing,
                BoundTypeClause.NullableDecimal),

        // any
        new BoundBinaryOperator(SyntaxKind.IsKeyword, BoundBinaryOperatorKind.Is,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxKind.IsntKeyword, BoundBinaryOperatorKind.Isnt,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundBinaryOperatorKind opType { get; }

    /// <summary>
    /// Left side operand type.
    /// </summary>
    internal BoundTypeClause leftType { get; }

    /// <summary>
    /// Right side operand type.
    /// </summary>
    internal BoundTypeClause rightType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Attempts to bind an operator with given sides.
    /// </summary>
    /// <param name="kind">Operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundBinaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundBinaryOperator Bind(SyntaxKind kind, BoundTypeClause leftType, BoundTypeClause rightType) {
        var nonNullableLeft = BoundTypeClause.NonNullable(leftType);
        var nonNullableRight = BoundTypeClause.NonNullable(rightType);

        foreach (var op in _operators) {
            var leftIsCorrect = Cast.Classify(nonNullableLeft, op.leftType).isImplicit;
            var rightIsCorrect = Cast.Classify(nonNullableRight, op.rightType).isImplicit;

            if (op.kind == kind && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}
