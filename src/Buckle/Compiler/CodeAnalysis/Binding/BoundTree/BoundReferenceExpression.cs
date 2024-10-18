
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound reference expression, bound from a <see cref="Syntax.ReferenceExpressionSyntax" />.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(BoundExpression expression, TypeSymbol type) {
        this.expression = expression;
        this.type = type;
    }

    internal BoundExpression expression { get; }

    internal override TypeSymbol type { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ReferenceExpression;

}
