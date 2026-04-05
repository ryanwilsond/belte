using System;
using System.Collections.Immutable;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        internal sealed class Not : Tests {
            internal readonly Tests negated;

            private Not(Tests negated) {
                this.negated = negated;
            }

            internal static Tests Create(Tests negated) {
                return negated switch {
                    True _ => False.Instance,
                    False _ => True.Instance,
                    Not n => n.negated,
                    AndSequence a => new Not(a),
                    OrSequence a => AndSequence.Create(NegateSequenceElements(a.remainingTests)),
                    One o => new Not(o),
                    _ => throw ExceptionUtilities.UnexpectedValue(negated),
                };
            }

            private static ArrayBuilder<Tests> NegateSequenceElements(ImmutableArray<Tests> seq) {
                var builder = ArrayBuilder<Tests>.GetInstance(seq.Length);

                foreach (var t in seq)
                    builder.Add(Create(t));

                return builder;
            }

            internal override Tests RemoveEvaluation(BoundDagEvaluation e) {
                return Create(negated.RemoveEvaluation(e));
            }

            internal override Tests RewriteNestedLengthTests() {
                return Create(negated.RewriteNestedLengthTests());
            }

            internal override BoundDagTest ComputeSelectedTest() {
                return negated.ComputeSelectedTest();
            }

            internal override string Dump(Func<BoundDagTest, string> dump) {
                return $"Not ({negated.Dump(dump)})";
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
                negated.Filter(
                    builder,
                    test,
                    state,
                    whenTrueValues,
                    whenFalseValues,
                    out var whenTestTrue,
                    out var whenTestFalse,
                    ref foundExplicitNullTest
                );

                whenTrue = Create(whenTestTrue);
                whenFalse = Create(whenTestFalse);
            }

            public override bool Equals(object? obj) {
                return this == obj || obj is Not n && negated.Equals(n.negated);
            }

            public override int GetHashCode() {
                return Hash.Combine(negated.GetHashCode(), typeof(Not).GetHashCode());
            }
        }
    }
}
