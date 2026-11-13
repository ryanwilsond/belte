#if DEBUG

using System.Collections.Generic;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class DebugBoundTreeVisitor : BoundTreeWalkerWithStackGuard {
    private readonly HashSet<BoundNode> _seenNodes = [];

    private DebugBoundTreeVisitor() { }

    internal static void EnsureNoReusedNodes(BoundNode node) {
        var visitor = new DebugBoundTreeVisitor();
        visitor.Visit(node);
    }

    internal override BoundNode Visit(BoundNode node) {
        if (!_seenNodes.Add(node))
            Debug.Assert(false);

        return base.Visit(node);
    }
}

#endif
