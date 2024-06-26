using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound unary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundUnaryOperator {
    private BoundUnaryOperator(
        SyntaxKind kind, BoundUnaryOperatorKind opKind, BoundType operandType, BoundType resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.operandType = operandType;
        type = resultType;
    }

    private BoundUnaryOperator(SyntaxKind kind, BoundUnaryOperatorKind opKind, BoundType operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundUnaryOperator[] Operators = {
        // integer
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            BoundType.Int),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            BoundType.Int),
        new BoundUnaryOperator(SyntaxKind.TildeToken, BoundUnaryOperatorKind.BitwiseCompliment,
            BoundType.Int),

        // boolean
        new BoundUnaryOperator(SyntaxKind.ExclamationToken, BoundUnaryOperatorKind.BooleanNegation,
            BoundType.Bool),

        // decimal
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            BoundType.Decimal),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            BoundType.Decimal),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundUnaryOperatorKind opKind { get; }

    internal BoundType operandType { get; }

    /// <summary>
    /// Result value <see cref="BoundType" />.
    /// </summary>
    internal BoundType type { get; }

    /// <summary>
    /// Attempts to bind an operator with given operand.
    /// </summary>
    /// <param name="kind">Operator <see cref="BoundType" />.</param>
    /// <param name="operandType">Operand <see cref="BoundType" />.</param>
    /// <returns><see cref="BoundUnaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundUnaryOperator Bind(SyntaxKind kind, BoundType operandType) {
        foreach (var op in Operators) {
            var operandIsCorrect = op.operandType is null
                ? true
                : Cast.Classify(operandType, op.operandType, false).isImplicit;

            if (op.kind == kind && operandIsCorrect) {
                if (op.operandType is null) {
                    return new BoundUnaryOperator(kind, op.opKind, operandType);
                } else if (operandType.isNullable) {
                    return new BoundUnaryOperator(
                        kind,
                        op.opKind,
                        BoundType.CopyWith(op.operandType, isNullable: true),
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
