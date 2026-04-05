using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private sealed class DecisionDag {
        internal readonly DagState rootNode;

        internal DecisionDag(DagState rootNode) {
            this.rootNode = rootNode;
        }

        private static void AddSuccessor(ref TemporaryArray<DagState> builder, DagState state) {
            builder.AddIfNotNull(state.trueBranch);
            builder.AddIfNotNull(state.falseBranch);
        }

        internal bool TryGetTopologicallySortedReachableStates(out ImmutableArray<DagState> result) {
            return TopologicalSort.TryIterativeSort(rootNode, AddSuccessor, out result);
        }
    }
}
