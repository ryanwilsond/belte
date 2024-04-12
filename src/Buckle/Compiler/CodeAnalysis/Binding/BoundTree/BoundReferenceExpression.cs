
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound reference expression, bound from a <see cref="Syntax.ReferenceExpressionSyntax" />.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundType type => BoundType.CopyWith(
        expression.type, isConstantReference: false, isReference: true, isExplicitReference: true
    );

    internal override BoundNodeKind kind => BoundNodeKind.ReferenceExpression;

}
