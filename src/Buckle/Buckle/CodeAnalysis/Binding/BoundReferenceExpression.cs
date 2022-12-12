using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound reference expression, bound from a parser ReferenceExpression.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(VariableSymbol variable, BoundTypeClause typeClause) {
        this.variable = variable;
        this.typeClause = typeClause;
    }

    internal VariableSymbol variable { get; }

    internal override BoundNodeType type => BoundNodeType.ReferenceExpression;

    internal override BoundTypeClause typeClause { get; }
}
