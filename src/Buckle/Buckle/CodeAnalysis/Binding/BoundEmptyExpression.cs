
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound empty expression, bound from a parser EmptyExpression.
/// Converted to NOP statements eventually.
/// </summary>
internal sealed class BoundEmptyExpression : BoundExpression {
    internal BoundEmptyExpression() { }

    internal override BoundNodeType type => BoundNodeType.EmptyExpression;

    internal override BoundTypeClause typeClause => null;
}
