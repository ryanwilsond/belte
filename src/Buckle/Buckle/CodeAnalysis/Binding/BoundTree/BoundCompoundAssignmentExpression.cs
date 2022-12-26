using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound compound assignment expression. No direct <see cref="Parser" /> equivalent.
/// Is bound from a parser <see cref="AssignmentExpressionSyntax" />.
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

    internal override BoundNodeKind kind => BoundNodeKind.CompoundAssignmentExpression;

    internal override BoundType type => right.type;
}
