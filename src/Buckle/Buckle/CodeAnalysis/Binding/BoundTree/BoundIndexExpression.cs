
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a <see cref="IndexExpressionSyntax" />.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression expression, BoundExpression index) {
        this.expression = expression;
        this.index = index;
    }

    internal BoundExpression expression { get; }

    internal BoundExpression index { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IndexExpression;

    internal override BoundType type => expression.type.ChildType();
}
