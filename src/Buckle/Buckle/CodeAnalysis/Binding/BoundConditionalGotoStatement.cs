
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound conditional goto statement, produced by Lowerer. No parser equivalent.
/// E.g. goto label if condition
/// </summary>
internal sealed class BoundConditionalGotoStatement : BoundStatement {
    internal BoundConditionalGotoStatement(BoundLabel label, BoundExpression condition, bool jumpIfTrue = true) {
        this.label = label;
        this.condition = condition;
        this.jumpIfTrue = jumpIfTrue;
    }

    internal BoundLabel label { get; }

    internal BoundExpression condition { get; }

    internal bool jumpIfTrue { get; }

    internal override BoundNodeType type => BoundNodeType.ConditionalGotoStatement;
}
