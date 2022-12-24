using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound unary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundUnaryOperator {
    private BoundUnaryOperator(
        SyntaxKind kind, BoundUnaryOperatorKind opKind, BoundTypeClause operandType, BoundTypeClause resultType) {
        this.kind = kind;
        this.opType = opKind;
        this.operandType = operandType;
        typeClause = resultType;
    }

    private BoundUnaryOperator(SyntaxKind kind, BoundUnaryOperatorKind opKind, BoundTypeClause operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundUnaryOperator[] _operators = {
        // integer
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxKind.TildeToken, BoundUnaryOperatorKind.BitwiseCompliment,
            BoundTypeClause.Int),

        // boolean
        new BoundUnaryOperator(SyntaxKind.ExclamationToken, BoundUnaryOperatorKind.BooleanNegation,
            BoundTypeClause.Bool),

        // decimal
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            BoundTypeClause.Decimal),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            BoundTypeClause.Decimal),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundUnaryOperatorKind opType { get; }

    internal BoundTypeClause operandType { get; }

    /// <summary>
    /// Result value <see cref="BoundTypeClause" />.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Attempts to bind an operator with given operand.
    /// </summary>
    /// <param name="kind">Operator <see cref="BoundTypeClause" />.</param>
    /// <param name="operandType">Operand <see cref="BoundTypeClause" />.</param>
    /// <returns><see cref="BoundUnaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundUnaryOperator Bind(SyntaxKind kind, BoundTypeClause operandType) {
        var nonNullableOperand = BoundTypeClause.NonNullable(operandType);

        foreach (var op in _operators) {
            var operandIsCorrect = Cast.Classify(nonNullableOperand, op.operandType).isImplicit;

            if (op.kind == kind && operandIsCorrect)
                return op;
        }

        return null;
    }
}
