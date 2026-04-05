using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        internal sealed class OrSequence : SequenceTests {
            private OrSequence(ImmutableArray<Tests> remainingTests) : base(remainingTests) { }

            internal override BoundDagTest ComputeSelectedTest() {
                return remainingTests[0].ComputeSelectedTest();
            }

            internal override Tests Update(ArrayBuilder<Tests> remainingTests) {
                return Create(remainingTests);
            }

            internal static Tests Create(Tests t1, Tests t2) {
                if (t1 is True)
                    return t1;

                if (t1 is False)
                    return t2;

                var builder = ArrayBuilder<Tests>.GetInstance(2);
                builder.Add(t1);
                builder.Add(t2);
                return Create(builder);
            }

            internal static Tests Create(ArrayBuilder<Tests> remainingTests) {
                for (var i = remainingTests.Count - 1; i >= 0; i--) {
                    switch (remainingTests[i]) {
                        case False _:
                            remainingTests.RemoveAt(i);
                            break;
                        case True t:
                            remainingTests.Free();
                            return t;
                        case OrSequence seq:
                            remainingTests.RemoveAt(i);
                            var testsToInsert = seq.remainingTests;

                            for (int j = 0, n = testsToInsert.Length; j < n; j++)
                                remainingTests.Insert(i + j, testsToInsert[j]);

                            break;
                    }
                }

                var result = remainingTests.Count switch {
                    0 => False.Instance,
                    1 => remainingTests[0],
                    _ => new OrSequence(remainingTests.ToImmutable()),
                };

                remainingTests.Free();
                return result;
            }

            internal override string Dump(Func<BoundDagTest, string> dump) {
                return $"OR({string.Join(", ", remainingTests.Select(t => t.Dump(dump)))})";
            }
        }
    }
}
