
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound postfix expression. Bound from a <see cref="PostfixExpressionSyntax" />.
/// Always gets rewritten by the <see cref="Lowerer" /> into a <see cref="BoundAssignmentExpression" />.
/// </summary>
internal sealed class BoundPostfixExpression : BoundExpression {
    internal BoundPostfixExpression(BoundExpression operand, bool isIncrement, bool isNullAssert, bool isOwnStatement) {
        this.operand = operand;
        this.isIncrement = isIncrement;
        this.isNullAssert = isNullAssert;
        this.isOwnStatement = isOwnStatement;
    }

    internal BoundExpression operand { get; }

    /// <summary>
    /// If the operation is an increment. If not, it is a decrement.
    /// </summary>
    internal bool isIncrement { get; }

    /// <summary>
    /// If the operation is a null assert operation. The <see cref="BoundPostfixExpression.isIncrement" /> property is
    /// only used if this one is false;
    /// </summary>
    internal bool isNullAssert { get; }

    /// <summary>
    /// If the expression is an expression statement, as if this is the case the <see cref="Lowerer" /> has to do less.
    /// </summary>
    internal bool isOwnStatement { get; }

    internal override BoundType type => operand.type;

    internal override BoundNodeKind kind => BoundNodeKind.PostfixExpression;
}
