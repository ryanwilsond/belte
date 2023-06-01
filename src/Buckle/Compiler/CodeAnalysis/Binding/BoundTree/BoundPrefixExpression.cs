
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound prefix expression. Bound from a <see cref="Syntax.PrefixExpressionSyntax" />.
/// Always gets rewritten by the <see cref="Lowering.Lowerer" /> into a <see cref="BoundAssignmentExpression" />.
/// </summary>
internal sealed class BoundPrefixExpression : BoundExpression {
    internal BoundPrefixExpression(BoundPrefixOperator op, BoundExpression operand) {
        this.op = op;
        this.operand = operand;
    }

    internal BoundPrefixOperator op { get; }

    internal BoundExpression operand { get; }

    internal override BoundType type => op.type;

    internal override BoundNodeKind kind => BoundNodeKind.PrefixExpression;
}
