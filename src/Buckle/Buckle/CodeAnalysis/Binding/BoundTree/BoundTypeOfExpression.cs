
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound typeof expression, bound from a <see cref="TypeOfExpressionSyntax" />.
/// </summary>
internal sealed class BoundTypeOfExpression : BoundExpression {
    internal BoundTypeOfExpression(BoundType type) {
        this.typeOfType = type;
    }

    internal BoundType typeOfType { get; }

    internal override BoundNodeKind kind => BoundNodeKind.TypeOfExpression;

    internal override BoundType type => BoundType.Type;
}
