using System;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private abstract partial class Tests {
        internal sealed class One : Tests {
            internal readonly BoundDagTest test;

            internal One(BoundDagTest test) {
                this.test = test;
            }

            internal void Deconstruct(out BoundDagTest test) {
                test = this.test;
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
                var syntax = test.syntax;
                var other = this.test;

                if (other is BoundDagEvaluation ||
                    !builder.CheckInputRelation(syntax, state, test, other,
                        relationCondition: out var relationCondition,
                        relationEffect: out var relationEffect)) {
                    whenTrue = whenFalse = this;
                    return;
                }

                builder.CheckConsistentDecision(
                    test: test,
                    other: other,
                    whenTrueValues: whenTrueValues,
                    whenFalseValues: whenFalseValues,
                    syntax: syntax,
                    trueTestPermitsTrueOther: out bool trueDecisionPermitsTrueOther,
                    falseTestPermitsTrueOther: out bool falseDecisionPermitsTrueOther,
                    trueTestImpliesTrueOther: out bool trueDecisionImpliesTrueOther,
                    falseTestImpliesTrueOther: out bool falseDecisionImpliesTrueOther,
                    foundExplicitNullTest: ref foundExplicitNullTest);

                whenTrue = Rewrite(trueDecisionImpliesTrueOther, trueDecisionPermitsTrueOther, relationCondition, relationEffect, this);
                whenFalse = Rewrite(falseDecisionImpliesTrueOther, falseDecisionPermitsTrueOther, relationCondition, relationEffect, this);

                static Tests Rewrite(
                    bool decisionImpliesTrueOther,
                    bool decisionPermitsTrueOther,
                    Tests relationCondition,
                    Tests relationEffect,
                    Tests other) {
                    return decisionImpliesTrueOther
                        ? OrSequence.Create(AndSequence.Create(relationCondition, relationEffect), other)
                        : !decisionPermitsTrueOther
                            ? AndSequence.Create(Not.Create(AndSequence.Create(relationCondition, relationEffect)), other)
                            : AndSequence.Create(OrSequence.Create(Not.Create(relationCondition), relationEffect), other);
                }
            }

            internal override BoundDagTest ComputeSelectedTest() {
                return test;
            }

            internal override Tests RemoveEvaluation(BoundDagEvaluation e) {
                return e.Equals(test) ? True.Instance : (Tests)this;
            }

            internal override string Dump(Func<BoundDagTest, string> dump) {
                return dump(test);
            }

            public override bool Equals(object obj) {
                return this == obj || obj is One other && test.Equals(other.test);
            }

            public override int GetHashCode() {
                return test.GetHashCode();
            }

            internal override Tests RewriteNestedLengthTests() {
                return this;
            }
        }
    }
}
