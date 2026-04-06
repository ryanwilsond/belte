using System;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        internal sealed class True : Tests {
            internal static readonly True Instance = new True();

            internal override string Dump(Func<BoundDagTest, string> dump) {
                return "TRUE";
            }

            internal override void Filter(
                DecisionDagBuilder builder,
                BoundDagTest test,
                DagState state,
                IValueSet? whenTrueValues,
                IValueSet? whenFalseValues,
                out Tests whenTrue,
                out Tests whenFalse,
                ref bool foundExplicitNullTest) {
                whenTrue = whenFalse = this;
            }
        }
    }
}
