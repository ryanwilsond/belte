using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound this expression, bound from a <see cref="Syntax.ThisExpressionSyntax" />.
/// </summary>
internal sealed class BoundThisExpression : BoundExpression {
    internal BoundThisExpression(TypeSymbol type) {
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ThisExpression;

    internal override TypeSymbol type { get; }
}
