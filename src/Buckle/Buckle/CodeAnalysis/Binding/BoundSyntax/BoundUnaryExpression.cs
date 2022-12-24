using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound unary expression, bound from a <see cref="UnaryExpressionSyntax" />.
/// </summary>
internal sealed class BoundUnaryExpression : BoundExpression {
    internal BoundUnaryExpression(BoundUnaryOperator op, BoundExpression operand) {
        this.op = op;
        this.operand = operand;
        constantValue = ConstantFolding.Fold(this.op, this.operand);
    }

    internal override BoundNodeKind kind => BoundNodeKind.UnaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundUnaryOperator op { get; }

    internal BoundExpression operand { get; }
}
