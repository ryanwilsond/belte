
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound empty expression, bound from a <see cref="EmptyExpressionSyntax" />.
/// Converted to NOP statements eventually.
/// </summary>
internal sealed class BoundEmptyExpression : BoundExpression {
    internal BoundEmptyExpression() { }

    internal override BoundNodeKind kind => BoundNodeKind.EmptyExpression;

    internal override BoundType type => null;
}
