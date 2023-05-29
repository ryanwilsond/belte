using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound postfix operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundPostfixOperator {
    private BoundPostfixOperator(
        SyntaxKind kind, BoundPostfixOperatorKind opKind, BoundType operandType, BoundType resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.operandType = operandType;
        type = resultType;
    }

    private BoundPostfixOperator(SyntaxKind kind, BoundPostfixOperatorKind opKind, BoundType operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundPostfixOperator[] _operators = {
        // integer
        new BoundPostfixOperator(SyntaxKind.PlusPlusToken, BoundPostfixOperatorKind.Increment, BoundType.Int),
        new BoundPostfixOperator(SyntaxKind.MinusMinusToken, BoundPostfixOperatorKind.Decrement, BoundType.Int),

        // decimal
        new BoundPostfixOperator(SyntaxKind.PlusPlusToken, BoundPostfixOperatorKind.Increment, BoundType.Decimal),
        new BoundPostfixOperator(SyntaxKind.MinusMinusToken, BoundPostfixOperatorKind.Decrement, BoundType.Decimal),

        new BoundPostfixOperator(SyntaxKind.ExclamationToken, BoundPostfixOperatorKind.NullAssert, null),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundPostfixOperatorKind opKind { get; }

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
    /// <returns><see cref="BoundPostfixOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundPostfixOperator Bind(SyntaxKind kind, BoundType operandType) {
        foreach (var op in _operators) {
            var operandIsCorrect = op.operandType is null
                ? true
                : Cast.Classify(operandType, op.operandType, false).isImplicit;

            if (op.kind == kind && operandIsCorrect) {
                if (op.operandType is null) {
                    return new BoundPostfixOperator(kind, op.opKind, operandType);
                } else if (operandType.isNullable) {
                    return new BoundPostfixOperator(
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
