
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound cast expression, bound from a <see cref="CastExpression" />.
/// In addition, a <see cref="BoundCastExpression" /> can be produced from a <see cref="CallExpression" /> using a type name as the function name.
/// E.g. int(3.4)
/// </summary>
internal sealed class BoundCastExpression : BoundExpression {
    internal BoundCastExpression(BoundTypeClause typeClause, BoundExpression expression) {
        this.typeClause = typeClause;

        if (expression is BoundLiteralExpression le)
            this.expression = new BoundLiteralExpression(expression.constantValue.value, this.typeClause);
        else
            this.expression = expression;

        constantValue = this.expression.constantValue;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.CastExpression;

    internal override BoundConstant constantValue { get; }

    internal override BoundTypeClause typeClause { get; }
}
