
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound typeof expression, bound from a <see cref="TypeOfExpression" />.
/// </summary>
internal sealed class BoundTypeOfExpression : BoundExpression {
    internal BoundTypeOfExpression(BoundTypeClause typeClause) {
        this.typeofTypeClause = typeClause;
    }

    internal BoundTypeClause typeofTypeClause { get; }

    internal override BoundNodeType type => BoundNodeType.TypeOfExpression;

    internal override BoundTypeClause typeClause => BoundTypeClause.Type;
}
