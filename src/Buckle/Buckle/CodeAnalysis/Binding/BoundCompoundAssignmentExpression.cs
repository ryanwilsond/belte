using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound compound assignment expression, bound from a parser CompoundAssignmentExpression.
/// All parser PrefixExpression and PostfixExpressions are converted to bound compound assignment expressions.
/// E.g. x++ -> x+=1
/// </summary>
internal sealed class BoundCompoundAssignmentExpression : BoundExpression {
    internal BoundCompoundAssignmentExpression(
        VariableSymbol variable, BoundBinaryOperator op, BoundExpression expression) {
        this.variable = variable;
        this.op = op;
        this.expression = expression;
    }

    internal VariableSymbol variable { get; }

    internal BoundBinaryOperator op { get; }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.CompoundAssignmentExpression;

    internal override BoundTypeClause typeClause => expression.typeClause;
}
