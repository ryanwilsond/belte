using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound compound assignment expression, bound from a <see cref="CompoundAssignmentExpression" />.
/// All <see cref="PrefixExpression" /> and <see cref="PostfixExpression" /> expressions are converted to
/// BoundCompoundAssignmentExpressions.<br/>
/// E.g.
/// <code>
/// x++
/// --->
/// x+=1
/// </code>
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
