
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound inline function expression, bound from a <see cref="InlineFunctionExpressionSyntax" />.
/// </summary>
internal sealed class BoundInlineFunctionExpression : BoundExpression {
    internal BoundInlineFunctionExpression(BoundBlockStatement body, BoundTypeClause returnType) {
        this.body = body;
        this.returnType = returnType;
    }

    internal BoundBlockStatement body { get; }

    internal BoundTypeClause returnType { get; }

    internal override BoundNodeKind kind => BoundNodeKind.InlineFunctionExpression;

    internal override BoundTypeClause typeClause => returnType;
}
