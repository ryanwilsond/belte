
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound expression, bound from a parser Expression.
/// All expressions have a possible constant value, used for constant folding.
/// If folding is not possible, constant value is null.
/// </summary>
internal abstract class BoundExpression : BoundNode {
    internal abstract BoundTypeClause typeClause { get; }

    internal virtual BoundConstant constantValue => null;
}
