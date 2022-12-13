using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound assignment expression, bound from a <see cref="AssignmentExpression" />.
/// </summary>
internal sealed class BoundAssignmentExpression : BoundExpression {
    internal BoundAssignmentExpression(VariableSymbol variable, BoundExpression expression) {
        this.variable = variable;
        this.expression = expression;
    }

    internal VariableSymbol variable { get; }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.AssignmentExpression;

    internal override BoundTypeClause typeClause => expression.typeClause;
}
