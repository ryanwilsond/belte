using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Block in the graph, represents a <see cref="BoundStatement" />.
/// </summary>
internal sealed class BasicBlock {
    internal List<BoundStatement> statements { get; } = new List<BoundStatement>();
    internal List<ControlFlowBranch> incoming { get; } = new List<ControlFlowBranch>();
    internal List<ControlFlowBranch> outgoing { get; } = new List<ControlFlowBranch>();
    internal bool isStart { get; }
    internal bool isEnd { get; }

    internal BasicBlock() {}

    internal BasicBlock(bool isStart) {
        this.isStart = isStart;
        isEnd = !isStart;
    }

    public override string ToString() {
        if (isStart)
            return "<Start>";

        if (isEnd)
            return "<End>";

        var builder = new StringBuilder();

        foreach (var statement in statements)
            builder.Append(DisplayText.DisplayNode(statement));

        return builder.ToString();
    }
}
