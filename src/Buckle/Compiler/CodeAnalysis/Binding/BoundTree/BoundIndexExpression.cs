
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a <see cref="Syntax.IndexExpressionSyntax" />.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression expression, BoundExpression index, bool isNullConditional) {
        this.expression = expression;
        this.index = index;
        this.isNullConditional = isNullConditional;
    }

    internal BoundExpression expression { get; }

    internal BoundExpression index { get; }

    internal bool isNullConditional { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IndexExpression;

    internal override BoundType type => expression.type.ChildType();
}
