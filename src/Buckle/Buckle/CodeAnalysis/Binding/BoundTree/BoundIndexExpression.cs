
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a <see cref="IndexExpressionSyntax" />.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression expression, BoundExpression index, bool isNullConditional) {
        this.operand = expression;
        this.index = index;
        this.isNullConditional = isNullConditional;
    }

    internal BoundExpression operand { get; }

    internal BoundExpression index { get; }

    internal bool isNullConditional { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IndexExpression;

    internal override BoundType type => operand.type.ChildType();
}
