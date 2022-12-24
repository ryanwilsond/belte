
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound cast expression, bound from a <see cref="CastExpressionSyntax" />.
/// In addition, a <see cref="BoundCastExpression" /> can be produced from a <see cref="CallExpressionSyntax" />
/// using a type name as the function name.<br/>
/// E.g.
/// <code>
/// int(3.4)
/// </code>
/// </summary>
internal sealed class BoundCastExpression : BoundExpression {
    internal BoundCastExpression(BoundTypeClause typeClause, BoundExpression expression) {
        this.typeClause = typeClause;
        this.expression = expression;
        constantValue = this.expression.constantValue;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CastExpression;

    internal override BoundConstant constantValue { get; }

    internal override BoundTypeClause typeClause { get; }
}
