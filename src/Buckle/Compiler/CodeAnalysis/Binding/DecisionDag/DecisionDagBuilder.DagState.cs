using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private sealed class DagState {
        private static readonly ObjectPool<DagState> DagStatePool
            = new ObjectPool<DagState>(static () => new DagState());

        internal ImmutableDictionary<BoundDagTemp, IValueSet> remainingValues { get; private set; } = null;

        internal FrozenArrayBuilder<StateForCase> cases { get; private set; }

        internal BoundDagTest selectedTest;

        internal DagState trueBranch, falseBranch;

        internal BoundDecisionDagNode dag;

        private DagState() { }

        internal static DagState GetInstance(
            FrozenArrayBuilder<StateForCase> cases,
            ImmutableDictionary<BoundDagTemp, IValueSet> remainingValues) {
            var dagState = DagStatePool.Allocate();
            dagState.cases = cases;
            dagState.remainingValues = remainingValues;
            return dagState;
        }

        internal void ClearAndFree() {
            cases.Free();
            cases = default;
            remainingValues = null;
            selectedTest = null;
            trueBranch = null;
            falseBranch = null;
            dag = null;

            DagStatePool.Free(this);
        }

        internal BoundDagTest ComputeSelectedTest() {
            return cases[0].remainingTests.ComputeSelectedTest();
        }

        internal void UpdateRemainingValues(ImmutableDictionary<BoundDagTemp, IValueSet> newRemainingValues) {
            remainingValues = newRemainingValues;
            selectedTest = null;
            trueBranch = null;
            falseBranch = null;
        }
    }
}
