
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound prefix expression. Bound from a <see cref="PrefixExpressionSyntax" />.
/// Always gets rewritten by the <see cref="Lowerer" /> into a <see cref="BoundAssignmentExpression" />.
/// </summary>
internal sealed class BoundPrefixExpression : BoundExpression {
    internal BoundPrefixExpression(BoundExpression operand, bool isIncrement) {
        this.operand = operand;
        this.isIncrement = isIncrement;
    }

    internal BoundExpression operand { get; }

    /// <summary>
    /// If the operation is an increment. If not, it is a decrement.
    /// </summary>
    internal bool isIncrement { get; }

    internal override BoundType type => operand.type;

    internal override BoundNodeKind kind => BoundNodeKind.PrefixExpression;
}
