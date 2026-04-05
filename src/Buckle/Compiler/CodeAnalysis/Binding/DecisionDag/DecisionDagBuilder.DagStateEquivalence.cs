using System.Collections.Generic;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private sealed class DagStateEquivalence : IEqualityComparer<DagState> {
        internal static readonly DagStateEquivalence Instance = new DagStateEquivalence();

        private DagStateEquivalence() { }

        public bool Equals(DagState? x, DagState? y) {
            if (x == y)
                return true;

            if (x.cases.Count != y.cases.Count)
                return false;

            for (int i = 0, n = x.cases.Count; i < n; i++) {
                if (!x.cases[i].Equals(y.cases[i]))
                    return false;
            }

            return true;
        }

        public int GetHashCode(DagState x) {
            var hashCode = 0;

            foreach (var value in x.cases)
                hashCode = Hash.Combine(value.GetHashCode(), hashCode);

            return Hash.Combine(hashCode, x.cases.Count);
        }
    }
}
