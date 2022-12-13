
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound expression, bound from a <see cref="Expression" />.
/// All expressions have a possible constant value, used for <see cref="ConstantFolding" />.
/// If folding is not possible, constant value is null.
/// </summary>
internal abstract class BoundExpression : BoundNode {
    internal abstract BoundTypeClause typeClause { get; }

    internal virtual BoundConstant constantValue => null;
}
