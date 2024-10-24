using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound ternary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundTernaryOperator {
    private BoundTernaryOperator(
        SyntaxKind leftOpKind,
        SyntaxKind rightOpKind,
        BoundTernaryOperatorKind opKind,
        TypeSymbol leftType,
        TypeSymbol centerType,
        TypeSymbol rightType,
        TypeSymbol resultType) {
        this.leftOpKind = leftOpKind;
        this.rightOpKind = rightOpKind;
        this.opKind = opKind;
        this.leftType = leftType;
        this.centerType = centerType;
        this.rightType = rightType;
        type = resultType;
    }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundTernaryOperator[] Operators = {
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            TypeSymbol.Bool, null, null, null),
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
    internal TypeSymbol leftType { get; }

    /// <summary>
    /// Center operand type.
    /// </summary>
    internal TypeSymbol centerType { get; }

    /// <summary>
    /// Right side operand type.
    /// </summary>
    internal TypeSymbol rightType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal TypeSymbol type { get; }

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
        foreach (var op in Operators) {
            var leftIsCorrect = op.leftType is null
                ? true
                : Conversion.Classify(leftType, op.leftType, false).isImplicit;

            var centerIsCorrect = op.centerType is null
                ? true
                : Conversion.Classify(centerType, op.centerType, false).isImplicit;

            var rightIsCorrect = op.rightType is null
                ? true
                : Conversion.Classify(rightType, op.rightType, false).isImplicit;

            if (op.leftOpKind == leftOpKind && op.rightOpKind == rightOpKind &&
                leftIsCorrect && rightIsCorrect && centerIsCorrect) {
                if (op.leftType is null || op.centerType is null || op.rightType is null) {
                    return new BoundTernaryOperator(
                        leftOpKind,
                        rightOpKind,
                        op.opKind,
                        op.leftType ?? leftType,
                        op.centerType ?? centerType,
                        op.rightType ?? rightType,
                        op.type ?? centerType
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
