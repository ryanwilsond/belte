
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a <see cref="Syntax.IndexExpressionSyntax" />.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression operand, BoundExpression index, bool isNullConditional) {
        this.operand = operand;
        this.index = index;
        this.isNullConditional = isNullConditional;
    }

    internal BoundExpression operand { get; }

    internal BoundExpression index { get; }

    internal bool isNullConditional { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IndexExpression;

    internal override BoundType type => operand.type.ChildType();
}
