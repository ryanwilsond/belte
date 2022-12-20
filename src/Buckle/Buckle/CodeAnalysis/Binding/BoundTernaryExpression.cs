using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All ternary operator types.
/// </summary>
internal enum BoundTernaryOperatorType {
    Conditional,
}

/// <summary>
/// Bound ternary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundTernaryOperator {
    private BoundTernaryOperator(
        SyntaxType leftOpType, SyntaxType rightOpType, BoundTernaryOperatorType opType, BoundTypeClause leftType,
        BoundTypeClause centerType, BoundTypeClause rightType, BoundTypeClause resultType) {
        this.leftOpType = leftOpType;
        this.rightOpType = rightOpType;
        this.opType = opType;
        this.leftType = leftType;
        this.centerType = centerType;
        this.rightType = rightType;
        typeClause = resultType;
    }

    private BoundTernaryOperator(
        SyntaxType leftOpType, SyntaxType rightOpType, BoundTernaryOperatorType opType,
        BoundTypeClause operandType, BoundTypeClause resultType)
        : this(leftOpType, rightOpType, opType, operandType, operandType, operandType, resultType) { }

    private BoundTernaryOperator(
        SyntaxType leftOpType, SyntaxType rightOpType, BoundTernaryOperatorType opType, BoundTypeClause typeClause)
        : this(leftOpType, rightOpType, opType, typeClause, typeClause, typeClause, typeClause) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundTernaryOperator[] _operators = {
        // integer
        new BoundTernaryOperator(SyntaxType.QuestionToken, SyntaxType.ColonToken, BoundTernaryOperatorType.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableInt,
            BoundTypeClause.NullableInt, BoundTypeClause.NullableInt),

        // boolean
        new BoundTernaryOperator(SyntaxType.QuestionToken, SyntaxType.ColonToken, BoundTernaryOperatorType.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableBool,
            BoundTypeClause.NullableBool, BoundTypeClause.NullableBool),

        // string
        new BoundTernaryOperator(SyntaxType.QuestionToken, SyntaxType.ColonToken, BoundTernaryOperatorType.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableString,
            BoundTypeClause.NullableString, BoundTypeClause.NullableString),

        // decimal
        new BoundTernaryOperator(SyntaxType.QuestionToken, SyntaxType.ColonToken, BoundTernaryOperatorType.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableDecimal,
            BoundTypeClause.NullableDecimal, BoundTypeClause.NullableDecimal),
    };

    /// <summary>
    /// Left operator token type.
    /// </summary>
    internal SyntaxType leftOpType { get; }

    /// <summary>
    /// Right operator token type.
    /// </summary>
    internal SyntaxType rightOpType { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundTernaryOperatorType opType { get; }

    /// <summary>
    /// Left side operand type.
    /// </summary>
    internal BoundTypeClause leftType { get; }

    /// <summary>
    /// Center operand type.
    /// </summary>
    internal BoundTypeClause centerType { get; }

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
    /// <param name="leftOpType">Left operator type.</param>
    /// <param name="rightOpType">Right operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="centerType">Center operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundTernaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundTernaryOperator Bind(SyntaxType leftOpType, SyntaxType rightOpType,
        BoundTypeClause leftType, BoundTypeClause centerType, BoundTypeClause rightType) {
        var nonNullableLeft = BoundTypeClause.NonNullable(leftType);
        var nonNullableCenter = BoundTypeClause.NonNullable(centerType);
        var nonNullableRight = BoundTypeClause.NonNullable(rightType);

        foreach (var op in _operators) {
            var leftIsCorrect = Cast.Classify(nonNullableLeft, op.leftType).isImplicit;
            var centerIsCorrect = Cast.Classify(nonNullableCenter, op.centerType).isImplicit;
            var rightIsCorrect = Cast.Classify(nonNullableRight, op.rightType).isImplicit;

            if (op.leftOpType == leftOpType && op.rightOpType == rightOpType && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}

/// <summary>
/// A bound ternary expression, bound from a <see cref="TernaryExpression" />.
/// </summary>
internal sealed class BoundTernaryExpression : BoundExpression {
    internal BoundTernaryExpression(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        this.left = left;
        this.op = op;
        this.center = center;
        this.right = right;
        constantValue = ConstantFolding.Fold(this.left, this.op, this.center, this.right);
    }

    internal override BoundNodeType type => BoundNodeType.TernaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundExpression left { get; }

    internal BoundTernaryOperator op { get; }

    internal BoundExpression center { get; }

    internal BoundExpression right { get; }
}
