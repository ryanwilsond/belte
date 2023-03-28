using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound ternary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundTernaryOperator {
    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind, BoundType leftType,
        BoundType centerType, BoundType rightType, BoundType resultType) {
        this.leftOpKind = leftOpKind;
        this.rightOpKind = rightOpKind;
        this.opKind = opKind;
        this.leftType = leftType;
        this.centerType = centerType;
        this.rightType = rightType;
        type = resultType;
    }

    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind,
        BoundType operandType, BoundType resultType)
        : this(leftOpKind, rightOpKind, opKind, operandType, operandType, operandType, resultType) { }

    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind, BoundType type)
        : this(leftOpKind, rightOpKind, opKind, type, type, type, type) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundTernaryOperator[] _operators = {
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            BoundType.Bool, null, null, null),
    };

    /// <summary>
    /// Left operator token type.
    /// </summary>
    internal SyntaxKind leftOpKind { get; }

    /// <summary>
    /// Right operator token type.
    /// </summary>
    internal SyntaxKind rightOpKind { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundTernaryOperatorKind opKind { get; }

    /// <summary>
    /// Left side operand type.
    /// </summary>
    internal BoundType leftType { get; }

    /// <summary>
    /// Center operand type.
    /// </summary>
    internal BoundType centerType { get; }

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
    /// <param name="leftOpKind">Left operator type.</param>
    /// <param name="rightOpKind">Right operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="centerType">Center operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundTernaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundTernaryOperator Bind(SyntaxKind leftOpKind, SyntaxKind rightOpKind,
        BoundType leftType, BoundType centerType, BoundType rightType) {
        foreach (var op in _operators) {
            var leftIsCorrect = op.leftType == null
                ? true
                : Cast.Classify(leftType, op.leftType, false).isImplicit;

            var centerIsCorrect = op.centerType == null
                ? true
                : Cast.Classify(centerType, op.centerType, false).isImplicit;

            var rightIsCorrect = op.rightType == null
                ? true
                : Cast.Classify(rightType, op.rightType, false).isImplicit;

            if (op.leftOpKind == leftOpKind && op.rightOpKind == rightOpKind &&
                leftIsCorrect && rightIsCorrect && centerIsCorrect) {
                if (op.leftType == null || op.centerType == null || op.rightType == null) {
                    return new BoundTernaryOperator(
                        leftOpKind,
                        rightOpKind,
                        op.opKind,
                        op.leftType == null ? leftType : op.leftType,
                        op.centerType == null ? centerType : op.centerType,
                        op.rightType == null ? rightType : op.rightType,
                        op.type == null ? centerType : op.type
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
