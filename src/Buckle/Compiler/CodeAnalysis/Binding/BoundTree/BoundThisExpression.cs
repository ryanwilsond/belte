
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound this expression, bound from a <see cref="Syntax.ThisExpressionSyntax" />.
/// </summary>
internal sealed class BoundThisExpression : BoundExpression {
    internal BoundThisExpression(BoundType type) {
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ThisExpression;

    internal override BoundType type { get; }
}
