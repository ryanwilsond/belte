using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All binary operator types.
/// </summary>
internal enum BoundBinaryOperatorType {
    Invalid,
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Power,
    LogicalAnd,
    LogicalOr,
    LogicalXor,
    LeftShift,
    RightShift,
    UnsignedRightShift,
    ConditionalAnd,
    ConditionalOr,
    EqualityEquals,
    EqualityNotEquals,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreatOrEqual,
    Is,
    Isnt,
    Modulo,
    NullCoalescing,
}

/// <summary>
/// Bound binary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundBinaryOperator {
    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType,
        BoundTypeClause leftType, BoundTypeClause rightType, BoundTypeClause resultType) {
        this.type = type;
        this.opType = opType;
        this.leftType = leftType;
        this.rightType = rightType;
        typeClause = resultType;
    }

    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType)
        : this(type, opType, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause typeClause)
        : this(type, opType, typeClause, typeClause, typeClause) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundBinaryOperator[] _operators = {
        // integer
        new BoundBinaryOperator(
            SyntaxType.PlusToken, BoundBinaryOperatorType.Addition, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.MinusToken, BoundBinaryOperatorType.Subtraction, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.AsteriskToken, BoundBinaryOperatorType.Multiplication, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.SlashToken, BoundBinaryOperatorType.Division, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.AsteriskAsteriskToken, BoundBinaryOperatorType.Power, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.AmpersandToken, BoundBinaryOperatorType.LogicalAnd, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.PipeToken, BoundBinaryOperatorType.LogicalOr, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.CaretToken, BoundBinaryOperatorType.LogicalXor, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.LessThanLessThanToken, BoundBinaryOperatorType.LeftShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.GreaterThanGreaterThanToken, BoundBinaryOperatorType.RightShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.GreaterThanGreaterThanGreaterThanToken,
            BoundBinaryOperatorType.UnsignedRightShift, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.EqualsEqualsToken, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.ExclamationEqualsToken, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LessThanToken, BoundBinaryOperatorType.LessThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GreaterThanToken, BoundBinaryOperatorType.GreaterThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LessThanEqualsToken, BoundBinaryOperatorType.LessOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GreaterThanEqualsToken, BoundBinaryOperatorType.GreatOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PercentToken, BoundBinaryOperatorType.Modulo, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.QuestionQuestionToken, BoundBinaryOperatorType.NullCoalescing,
            BoundTypeClause.NullableInt),

        // boolean
        new BoundBinaryOperator(SyntaxType.AmpersandAmpersandToken, BoundBinaryOperatorType.ConditionalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PipePipeToken, BoundBinaryOperatorType.ConditionalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.AmpersandToken, BoundBinaryOperatorType.LogicalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PipeToken, BoundBinaryOperatorType.LogicalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.CaretToken, BoundBinaryOperatorType.LogicalXor,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EqualsEqualsToken, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.ExclamationEqualsToken, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.QuestionQuestionToken, BoundBinaryOperatorType.NullCoalescing,
            BoundTypeClause.NullableBool),

        // string
        new BoundBinaryOperator(SyntaxType.PlusToken, BoundBinaryOperatorType.Addition,
            BoundTypeClause.String),
        new BoundBinaryOperator(SyntaxType.EqualsEqualsToken, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.ExclamationEqualsToken, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.QuestionQuestionToken, BoundBinaryOperatorType.NullCoalescing,
            BoundTypeClause.NullableString),

        // decimal
        new BoundBinaryOperator(SyntaxType.PlusToken, BoundBinaryOperatorType.Addition,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.MinusToken, BoundBinaryOperatorType.Subtraction,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.AsteriskToken, BoundBinaryOperatorType.Multiplication,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.SlashToken, BoundBinaryOperatorType.Division,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.AsteriskAsteriskToken, BoundBinaryOperatorType.Power,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.EqualsEqualsToken, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.ExclamationEqualsToken, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LessThanToken, BoundBinaryOperatorType.LessThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GreaterThanToken, BoundBinaryOperatorType.GreaterThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LessThanEqualsToken, BoundBinaryOperatorType.LessOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GreaterThanEqualsToken, BoundBinaryOperatorType.GreatOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PercentToken, BoundBinaryOperatorType.Modulo, BoundTypeClause.Decimal),
    new BoundBinaryOperator(SyntaxType.QuestionQuestionToken, BoundBinaryOperatorType.NullCoalescing,
            BoundTypeClause.NullableDecimal),

        // any
        new BoundBinaryOperator(SyntaxType.IsKeyword, BoundBinaryOperatorType.Is,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.IsntKeyword, BoundBinaryOperatorType.Isnt,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
    };

    internal SyntaxType type { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundBinaryOperatorType opType { get; }

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
    /// <param name="type">Operator type</param>
    /// <param name="leftType">Left operand type</param>
    /// <param name="rightType">Right operand type</param>
    /// <returns>Bound operator if an operator exists, otherwise null</returns>
    internal static BoundBinaryOperator Bind(SyntaxType type, BoundTypeClause leftType, BoundTypeClause rightType) {
        var nonNullableLeft = BoundTypeClause.NonNullable(leftType);
        var nonNullableRight = BoundTypeClause.NonNullable(rightType);

        foreach (var op in _operators) {
            var leftIsCorrect = Cast.Classify(nonNullableLeft, op.leftType).isImplicit;
            var rightIsCorrect = Cast.Classify(nonNullableRight, op.rightType).isImplicit;

            if (op.type == type && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}

/// <summary>
/// A bound binary expression, bound from a parser BinaryExpression.
/// </summary>
internal sealed class BoundBinaryExpression : BoundExpression {
    internal BoundBinaryExpression(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        this.left = left;
        this.op = op;
        this.right = right;
        constantValue = ConstantFolding.Fold(this.left, this.op, this.right);
    }

    internal override BoundNodeType type => BoundNodeType.BinaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundExpression left { get; }

    internal BoundBinaryOperator op { get; }

    internal BoundExpression right { get; }
}
