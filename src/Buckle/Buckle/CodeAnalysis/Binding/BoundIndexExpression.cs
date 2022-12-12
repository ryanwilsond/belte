
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a parser IndexExpression.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression expression, BoundExpression index) {
        this.expression = expression;
        this.index = index;
    }

    internal BoundExpression expression { get; }

    internal BoundExpression index { get; }

    internal override BoundNodeType type => BoundNodeType.IndexExpression;

    internal override BoundTypeClause typeClause => expression.typeClause.ChildType();
}
