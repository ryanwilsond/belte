
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound postfix expression. Bound from a <see cref="Syntax.PostfixExpressionSyntax" />.
/// Always gets rewritten by the <see cref="Lowering.Lowerer" /> into a <see cref="BoundAssignmentExpression" />.
/// </summary>
internal sealed class BoundPostfixExpression : BoundExpression {
    internal BoundPostfixExpression(BoundExpression operand, BoundPostfixOperator op, bool isOwnStatement) {
        this.operand = operand;
        this.op = op;
        this.isOwnStatement = isOwnStatement;
    }

    internal BoundExpression operand { get; }

    internal BoundPostfixOperator op { get; }

    /// <summary>
    /// If the expression is an expression statement, as if this is the case the <see cref="Lowering.Lowerer" /> has to
    /// do less.
    /// </summary>
    internal bool isOwnStatement { get; }

    internal override BoundType type => op.type;

    internal override BoundNodeKind kind => BoundNodeKind.PostfixExpression;
}
