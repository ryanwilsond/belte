using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        private Tests() { }

        internal abstract void Filter(
            DecisionDagBuilder builder,
            BoundDagTest test,
            DagState state,
            IValueSet? whenTrueValues,
            IValueSet? whenFalseValues,
            out Tests whenTrue,
            out Tests whenFalse,
            ref bool foundExplicitNullTest);

        internal virtual BoundDagTest ComputeSelectedTest() {
            throw ExceptionUtilities.Unreachable();
        }

        internal virtual Tests RemoveEvaluation(BoundDagEvaluation e) {
            return this;
        }

        internal virtual Tests RewriteNestedLengthTests() {
            return this;
        }

        internal abstract string Dump(Func<BoundDagTest, string> dump);
    }
}
