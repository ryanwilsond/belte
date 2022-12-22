using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound assignment expression, bound from a <see cref="AssignmentExpression" />.
/// </summary>
internal sealed class BoundAssignmentExpression : BoundExpression {
    internal BoundAssignmentExpression(BoundExpression left, BoundExpression right) {
        this.left = left;
        this.right = right;
    }

    internal BoundExpression left { get; }

    internal BoundExpression right { get; }

    internal override BoundNodeType type => BoundNodeType.AssignmentExpression;

    internal override BoundTypeClause typeClause => right.typeClause;
}
