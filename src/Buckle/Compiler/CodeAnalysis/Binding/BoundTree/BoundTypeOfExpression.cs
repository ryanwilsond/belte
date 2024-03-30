
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound typeof expression, bound from a <see cref="Syntax.TypeOfExpressionSyntax" />.
/// </summary>
internal sealed class BoundTypeOfExpression : BoundExpression {
    internal BoundTypeOfExpression(BoundType type) {
        typeOfType = type;
    }

    internal BoundType typeOfType { get; }

    internal override BoundNodeKind kind => BoundNodeKind.TypeOfExpression;

    internal override BoundType type => BoundType.CopyWith(BoundType.Type, isLiteral: true);
}
