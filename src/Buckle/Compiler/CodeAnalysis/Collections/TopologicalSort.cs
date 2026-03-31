using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal delegate void TopologicalSortAddSuccessors<TNode>(ref TemporaryArray<TNode> builder, TNode node);

internal static class TopologicalSort {
    internal static bool TryIterativeSort<TNode>(
        TNode node,
        TopologicalSortAddSuccessors<TNode> addSuccessors,
        out ImmutableArray<TNode> result)
        where TNode : notnull {
        return TryIterativeSort(SpecializedCollections.SingletonEnumerable(node), addSuccessors, out result);
    }

    internal static bool TryIterativeSort<TNode>(
        IEnumerable<TNode> nodes,
        TopologicalSortAddSuccessors<TNode> addSuccessors,
        out ImmutableArray<TNode> result)
        where TNode : notnull {
        var predecessorCounts = PredecessorCounts(nodes, addSuccessors, out var allNodes);

        using var successors = TemporaryArray<TNode>.Empty;

        var ready = ArrayBuilder<TNode>.GetInstance();

        foreach (var node in allNodes) {
            if (predecessorCounts[node] == 0)
                ready.Push(node);
        }

        var resultBuilder = ArrayBuilder<TNode>.GetInstance();

        while (ready.Count != 0) {
            var node = ready.Pop();
            resultBuilder.Add(node);

            successors.Clear();
            addSuccessors(ref successors.AsRef(), node);

            foreach (var succ in successors) {
                var count = predecessorCounts[succ];
                predecessorCounts[succ] = count - 1;

                if (count == 1)
                    ready.Push(succ);
            }
        }

        var hadCycle = predecessorCounts.Count != resultBuilder.Count;
        result = hadCycle ? [] : resultBuilder.ToImmutable();

        predecessorCounts.Free();
        ready.Free();
        resultBuilder.Free();

        return !hadCycle;
    }

    private static PooledDictionary<TNode, int> PredecessorCounts<TNode>(
        IEnumerable<TNode> nodes,
        TopologicalSortAddSuccessors<TNode> addSuccessors,
        out ImmutableArray<TNode> allNodes)
        where TNode : notnull {
        var predecessorCounts = PooledDictionary<TNode, int>.GetInstance();
        var counted = PooledHashSet<TNode>.GetInstance();
        var toCount = ArrayBuilder<TNode>.GetInstance();
        var allNodesBuilder = ArrayBuilder<TNode>.GetInstance();
        using var successors = TemporaryArray<TNode>.Empty;

        toCount.AddRange(nodes);

        while (toCount.Count != 0) {
            var n = toCount.Pop();

            if (!counted.Add(n))
                continue;

            allNodesBuilder.Add(n);

            if (!predecessorCounts.ContainsKey(n))
                predecessorCounts.Add(n, 0);

            successors.Clear();
            addSuccessors(ref successors.AsRef(), n);

            foreach (var succ in successors) {
                toCount.Push(succ);

                if (predecessorCounts.TryGetValue(succ, out var succPredecessorCount))
                    predecessorCounts[succ] = succPredecessorCount + 1;
                else
                    predecessorCounts.Add(succ, 1);
            }
        }

        counted.Free();
        toCount.Free();
        allNodes = allNodesBuilder.ToImmutableAndFree();
        return predecessorCounts;
    }
}
