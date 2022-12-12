using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All unary operator types.
/// </summary>
internal enum BoundUnaryOperatorType {
    Invalid,
    NumericalIdentity,
    NumericalNegation,
    BooleanNegation,
    BitwiseCompliment,
}

/// <summary>
/// Bound unary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundUnaryOperator {
    private BoundUnaryOperator(
        SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType) {
        this.type = type;
        this.opType = opType;
        this.operandType = operandType;
        typeClause = resultType;
    }

    private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType)
        : this(type, opType, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundUnaryOperator[] _operators = {
        // integer
        new BoundUnaryOperator(SyntaxType.PlusToken, BoundUnaryOperatorType.NumericalIdentity,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxType.MinusToken, BoundUnaryOperatorType.NumericalNegation,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxType.TildeToken, BoundUnaryOperatorType.BitwiseCompliment,
            BoundTypeClause.Int),

        // boolean
        new BoundUnaryOperator(SyntaxType.ExclamationToken, BoundUnaryOperatorType.BooleanNegation,
            BoundTypeClause.Bool),

        // decimal
        new BoundUnaryOperator(SyntaxType.PlusToken, BoundUnaryOperatorType.NumericalIdentity,
            BoundTypeClause.Decimal),
        new BoundUnaryOperator(SyntaxType.MinusToken, BoundUnaryOperatorType.NumericalNegation,
            BoundTypeClause.Decimal),
    };

    internal SyntaxType type { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundUnaryOperatorType opType { get; }

    internal BoundTypeClause operandType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Attempts to bind an operator with given operand.
    /// </summary>
    /// <param name="type">Operator type</param>
    /// <param name="operandType">Operand type</param>
    /// <returns>Bound operator if an operator exists, otherwise null</returns>
    internal static BoundUnaryOperator Bind(SyntaxType type, BoundTypeClause operandType) {
        var nonNullableOperand = BoundTypeClause.NonNullable(operandType);

        foreach (var op in _operators) {
            var operandIsCorrect = Cast.Classify(nonNullableOperand, op.operandType).isImplicit;

            if (op.type == type && operandIsCorrect)
                return op;
        }

        return null;
    }
}

/// <summary>
/// A bound unary expression, bound from a parser UnaryExpression.
/// </summary>
internal sealed class BoundUnaryExpression : BoundExpression {
    internal BoundUnaryExpression(BoundUnaryOperator op, BoundExpression operand) {
        this.op = op;
        this.operand = operand;
        constantValue = ConstantFolding.Fold(this.op, this.operand);
    }

    internal override BoundNodeType type => BoundNodeType.UnaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundUnaryOperator op { get; }

    internal BoundExpression operand { get; }
}
