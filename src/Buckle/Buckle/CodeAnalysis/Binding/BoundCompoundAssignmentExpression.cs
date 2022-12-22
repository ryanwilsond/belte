using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound compound assignment expression, bound from a <see cref="CompoundAssignmentExpression" />.
/// All <see cref="PrefixExpression" /> and <see cref="PostfixExpression" /> expressions are converted to
/// BoundCompoundAssignmentExpressions.<br/>
/// E.g.
/// <code>
/// x++
/// --->
/// x+=1
/// </code>
/// </summary>
internal sealed class BoundCompoundAssignmentExpression : BoundExpression {
    internal BoundCompoundAssignmentExpression(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        this.left = left;
        this.op = op;
        this.right = right;
    }

    internal BoundExpression left { get; }

    internal BoundBinaryOperator op { get; }

    internal BoundExpression right { get; }

    internal override BoundNodeType type => BoundNodeType.CompoundAssignmentExpression;

    internal override BoundTypeClause typeClause => right.typeClause;
}
