
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound this expression, bound from a <see cref="Syntax.BaseExpressionSyntax" />.
/// </summary>
internal sealed class BoundBaseExpression : BoundExpression {
    internal BoundBaseExpression(BoundType type) {
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.BaseExpression;

    internal override BoundType type { get; }
}
