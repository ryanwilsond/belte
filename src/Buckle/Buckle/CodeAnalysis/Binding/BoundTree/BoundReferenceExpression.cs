using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound reference expression, bound from a <see cref="ReferenceExpressionSyntax" />.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(VariableSymbol variable) {
        this.variable = variable;
    }

    internal VariableSymbol variable { get; }

    internal override BoundType type => BoundType.ExplicitReference(variable.type);

    internal override BoundNodeKind kind => BoundNodeKind.ReferenceExpression;

}
