
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound compound assignment expression. No direct <see cref="Syntax.InternalSyntax.Parser" /> equivalent.
/// Is bound from a parser <see cref="Syntax.AssignmentExpressionSyntax" />.
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
