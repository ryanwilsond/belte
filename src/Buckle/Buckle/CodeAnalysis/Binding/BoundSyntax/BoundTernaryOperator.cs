using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound ternary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundTernaryOperator {
    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind, BoundTypeClause leftType,
        BoundTypeClause centerType, BoundTypeClause rightType, BoundTypeClause resultType) {
        this.leftOpKind = leftOpKind;
        this.rightOpKind = rightOpKind;
        this.opType = opKind;
        this.leftType = leftType;
        this.centerType = centerType;
        this.rightType = rightType;
        typeClause = resultType;
    }

    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind,
        BoundTypeClause operandType, BoundTypeClause resultType)
        : this(leftOpKind, rightOpKind, opKind, operandType, operandType, operandType, resultType) { }

    private BoundTernaryOperator(
        SyntaxKind leftOpKind, SyntaxKind rightOpKind, BoundTernaryOperatorKind opKind, BoundTypeClause typeClause)
        : this(leftOpKind, rightOpKind, opKind, typeClause, typeClause, typeClause, typeClause) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
    internal static BoundTernaryOperator[] _operators = {
        // integer
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableInt,
            BoundTypeClause.NullableInt, BoundTypeClause.NullableInt),

        // boolean
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableBool,
            BoundTypeClause.NullableBool, BoundTypeClause.NullableBool),

        // string
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableString,
            BoundTypeClause.NullableString, BoundTypeClause.NullableString),

        // decimal
        new BoundTernaryOperator(SyntaxKind.QuestionToken, SyntaxKind.ColonToken, BoundTernaryOperatorKind.Conditional,
            BoundTypeClause.Bool, BoundTypeClause.NullableDecimal,
            BoundTypeClause.NullableDecimal, BoundTypeClause.NullableDecimal),
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
    internal BoundTernaryOperatorKind opType { get; }

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
    /// <param name="leftOpKind">Left operator type.</param>
    /// <param name="rightOpKind">Right operator type.</param>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="centerType">Center operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns><see cref="BoundTernaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundTernaryOperator Bind(SyntaxKind leftOpKind, SyntaxKind rightOpKind,
        BoundTypeClause leftType, BoundTypeClause centerType, BoundTypeClause rightType) {
        var nonNullableLeft = BoundTypeClause.NonNullable(leftType);
        var nonNullableCenter = BoundTypeClause.NonNullable(centerType);
        var nonNullableRight = BoundTypeClause.NonNullable(rightType);

        foreach (var op in _operators) {
            var leftIsCorrect = Cast.Classify(nonNullableLeft, op.leftType).isImplicit;
            var centerIsCorrect = Cast.Classify(nonNullableCenter, op.centerType).isImplicit;
            var rightIsCorrect = Cast.Classify(nonNullableRight, op.rightType).isImplicit;

            if (op.leftOpKind == leftOpKind && op.rightOpKind == rightOpKind && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}
