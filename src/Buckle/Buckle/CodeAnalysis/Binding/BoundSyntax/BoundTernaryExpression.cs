using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound ternary expression, bound from a <see cref="TernaryExpressionSyntax" />.
/// </summary>
internal sealed class BoundTernaryExpression : BoundExpression {
    internal BoundTernaryExpression(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        this.left = left;
        this.op = op;
        this.center = center;
        this.right = right;
        constantValue = ConstantFolding.Fold(this.left, this.op, this.center, this.right);
    }

    internal override BoundNodeKind kind => BoundNodeKind.TernaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundExpression left { get; }

    internal BoundTernaryOperator op { get; }

    internal BoundExpression center { get; }

    internal BoundExpression right { get; }
}
