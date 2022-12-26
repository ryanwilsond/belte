using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound reference expression, bound from a <see cref="ReferenceExpressionSyntax" />.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(VariableSymbol variable, BoundType type) {
        this.variable = variable;
        this.type = type;
    }

    internal VariableSymbol variable { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ReferenceExpression;

    internal override BoundType type { get; }
}
