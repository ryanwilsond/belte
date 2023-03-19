
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound assignment expression, bound from a <see cref="Syntax.AssignmentExpressionSyntax" />.
/// </summary>
internal sealed class BoundAssignmentExpression : BoundExpression {
    internal BoundAssignmentExpression(BoundExpression left, BoundExpression right) {
        this.left = left;
        this.right = right;
    }

    internal BoundExpression left { get; }

    internal BoundExpression right { get; }

    internal override BoundNodeKind kind => BoundNodeKind.AssignmentExpression;

    internal override BoundType type => right.type;
}
