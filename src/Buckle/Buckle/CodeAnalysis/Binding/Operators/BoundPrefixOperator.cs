using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound prefix operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundPrefixOperator {
    private BoundPrefixOperator(
        SyntaxKind kind, BoundPrefixOperatorKind opKind, BoundType operandType, BoundType resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.operandType = operandType;
        type = resultType;
    }

    private BoundPrefixOperator(SyntaxKind kind, BoundPrefixOperatorKind opKind, BoundType operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundPrefixOperator[] _operators = {
        // integer
        new BoundPrefixOperator(SyntaxKind.PlusPlusToken, BoundPrefixOperatorKind.Increment,
            BoundType.Int),
        new BoundPrefixOperator(SyntaxKind.MinusMinusToken, BoundPrefixOperatorKind.Decrement,
            BoundType.Int),

        // decimal
        new BoundPrefixOperator(SyntaxKind.PlusPlusToken, BoundPrefixOperatorKind.Increment,
            BoundType.Decimal),
        new BoundPrefixOperator(SyntaxKind.MinusMinusToken, BoundPrefixOperatorKind.Decrement,
            BoundType.Decimal),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundPrefixOperatorKind opKind { get; }

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
    /// <returns><see cref="BoundPrefixOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundPrefixOperator Bind(SyntaxKind kind, BoundType operandType) {
        foreach (var op in _operators) {
            var operandIsCorrect = op.operandType == null
                ? true
                : Cast.Classify(operandType, op.operandType, false).isImplicit;

            if (op.kind == kind && operandIsCorrect) {
                if (op.operandType == null) {
                    return new BoundPrefixOperator(kind, op.opKind, operandType);
                } else if (operandType.isNullable) {
                    return new BoundPrefixOperator(
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
