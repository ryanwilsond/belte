using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        public abstract class SequenceTests : Tests {
            internal readonly ImmutableArray<Tests> remainingTests;

            private protected SequenceTests(ImmutableArray<Tests> remainingTests) {
                this.remainingTests = remainingTests;
            }

            internal abstract Tests Update(ArrayBuilder<Tests> remainingTests);

            internal sealed override void Filter(
                DecisionDagBuilder builder,
                BoundDagTest test,
                DagState state,
                IValueSet? whenTrueValues,
                IValueSet? whenFalseValues,
                out Tests whenTrue,
                out Tests whenFalse,
                ref bool foundExplicitNullTest) {
                var trueBuilder = ArrayBuilder<Tests>.GetInstance(remainingTests.Length);
                var falseBuilder = ArrayBuilder<Tests>.GetInstance(remainingTests.Length);

                foreach (var other in remainingTests) {
                    other.Filter(
                        builder,
                        test,
                        state,
                        whenTrueValues,
                        whenFalseValues,
                        out var oneTrue,
                        out var oneFalse,
                        ref foundExplicitNullTest
                    );

                    trueBuilder.Add(oneTrue);
                    falseBuilder.Add(oneFalse);
                }

                whenTrue = Update(trueBuilder);
                whenFalse = Update(falseBuilder);
            }

            internal sealed override Tests RemoveEvaluation(BoundDagEvaluation e) {
                var builder = ArrayBuilder<Tests>.GetInstance(remainingTests.Length);

                foreach (var test in remainingTests)
                    builder.Add(test.RemoveEvaluation(e));

                return Update(builder);
            }

            internal sealed override Tests RewriteNestedLengthTests() {
                var builder = ArrayBuilder<Tests>.GetInstance(remainingTests.Length);

                foreach (var test in remainingTests)
                    builder.Add(test.RewriteNestedLengthTests());

                return Update(builder);
            }

            public sealed override bool Equals(object? obj) {
                return this == obj || obj is SequenceTests other &&
                    GetType() == other.GetType() && remainingTests.SequenceEqual(other.remainingTests);
            }

            public sealed override int GetHashCode() {
                var length = remainingTests.Length;
                var value = Hash.Combine(length, GetType().GetHashCode());
                value = Hash.Combine(Hash.CombineValues(remainingTests), value);
                return value;
            }
        }
    }
}
