using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Branch in the graph, represents code continuing from one <see cref="BoundStatement" /> to another.
/// </summary>
internal sealed class ControlFlowBranch {
    internal ControlFlowBranch(BasicBlock from, BasicBlock to, BoundExpression condition, FlowState flowState) {
        this.from = from;
        this.to = to;
        this.condition = condition;
        this.flowState = flowState;
    }

    internal BasicBlock from { get; }

    internal BasicBlock to { get; }

    internal BoundExpression condition { get; }

    internal FlowState flowState { get; }

    public override string ToString() {
        if (condition is null)
            return "";

        return condition.ToString();
    }
}
