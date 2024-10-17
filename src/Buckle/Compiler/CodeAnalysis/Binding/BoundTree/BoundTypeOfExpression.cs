using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound typeof expression, bound from a <see cref="Syntax.TypeOfExpressionSyntax" />.
/// </summary>
internal sealed class BoundTypeOfExpression : BoundExpression {
    internal BoundTypeOfExpression(TypeSymbol type) {
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.TypeOfExpression;

    internal override TypeSymbol type { get; }
}
