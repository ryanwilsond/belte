using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Block in the graph, represents a <see cref="BoundStatement" />.
/// </summary>
internal sealed class BasicBlock {
    internal BasicBlock(int capacity) {
        incomingAssignment = BitVector.AllSet(capacity);
        outgoingAssignment = BitVector.AllSet(capacity);
    }

    internal BasicBlock(bool isStart) {
        this.isStart = isStart;
        isEnd = !isStart;
    }

    internal List<BoundStatement> statements { get; } = [];

    internal List<ControlFlowBranch> incoming { get; } = [];

    internal List<ControlFlowBranch> outgoing { get; } = [];

    internal BitVector incomingAssignment { get; set; } = BitVector.Empty;

    internal BitVector outgoingAssignment { get; set; } = BitVector.Empty;

    internal bool isStart { get; }

    internal bool isEnd { get; }

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
