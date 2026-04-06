using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private static readonly ObjectPool<PooledDictionary<DagState, DagState>> UniqueStatePool =
        PooledDictionary<DagState, DagState>.CreatePool(DagStateEquivalence.Instance);

    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly LabelSymbol _defaultLabel;
    private readonly bool _forLowering;

    private DecisionDagBuilder(
        LabelSymbol defaultLabel,
        bool forLowering,
        BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;
        _defaultLabel = defaultLabel;
        _forLowering = forLowering;
    }

    internal static BoundDecisionDag CreateDecisionDagForSwitchStatement(
        SyntaxNode syntax,
        BoundExpression switchGoverningExpression,
        ImmutableArray<BoundSwitchSection> switchSections,
        LabelSymbol defaultLabel,
        BelteDiagnosticQueue diagnostics,
        bool forLowering = false) {
        var builder = new DecisionDagBuilder(defaultLabel, forLowering, diagnostics);
        return builder.CreateDecisionDagForSwitchStatement(syntax, switchGoverningExpression, switchSections);
    }

    private BoundDecisionDag CreateDecisionDagForSwitchStatement(
        SyntaxNode syntax,
        BoundExpression switchGoverningExpression,
        ImmutableArray<BoundSwitchSection> switchSections) {
        var rootIdentifier = BoundDagTemp.ForOriginalInput(switchGoverningExpression);
        var i = 0;
        using var builder = TemporaryArray<StateForCase>.GetInstance(switchSections.Length);

        foreach (var section in switchSections) {
            foreach (var label in section.switchLabels) {
                if (label.syntax.kind != SyntaxKind.DefaultSwitchLabel)
                    builder.Add(MakeTestsForPattern(++i, label.syntax, rootIdentifier, label.pattern, label.label));
            }
        }

        return MakeBoundDecisionDag(syntax, ref builder.AsRef());
    }

    private BoundDecisionDag MakeBoundDecisionDag(SyntaxNode syntax, ref TemporaryArray<StateForCase> cases) {
        var uniqueState = UniqueStatePool.Allocate();
        var decisionDag = MakeDecisionDag(ref cases, uniqueState);

        var defaultDecision = new BoundLeafDecisionDagNode(syntax, _defaultLabel);
        ComputeBoundDecisionDagNodes(decisionDag, defaultDecision);

        var rootDecisionDagNode = decisionDag.rootNode.dag;
        var boundDecisionDag = new BoundDecisionDag(rootDecisionDagNode.syntax, rootDecisionDagNode);

        foreach (var kvp in uniqueState)
            kvp.Key.ClearAndFree();

        uniqueState.Free();
        return boundDecisionDag;
    }

    private DecisionDag MakeDecisionDag(
        ref TemporaryArray<StateForCase> casesForRootNode,
        Dictionary<DagState, DagState> uniqueState) {
        using var workList = TemporaryArray<DagState>.Empty;

        DagState UniquifyState(
            FrozenArrayBuilder<StateForCase> cases,
            ImmutableDictionary<BoundDagTemp, IValueSet> remainingValues) {
            var state = DagState.GetInstance(cases, remainingValues);

            if (uniqueState.TryGetValue(state, out var existingState)) {
                state.ClearAndFree();
                state = null;

                var newRemainingValues = ImmutableDictionary.CreateBuilder<BoundDagTemp, IValueSet>();
                foreach (var (dagTemp, valuesForTemp) in remainingValues) {
                    if (existingState.remainingValues.TryGetValue(dagTemp, out var existingValuesForTemp)) {
                        var newExistingValuesForTemp = existingValuesForTemp.Union(valuesForTemp);
                        newRemainingValues.Add(dagTemp, newExistingValuesForTemp);
                    }
                }

                if (existingState.remainingValues.Count != newRemainingValues.Count ||
                    !Enumerable.All(
                        existingState.remainingValues,
                        kv => newRemainingValues.TryGetValue(kv.Key, out var values) && kv.Value.Equals(values))) {
                    existingState.UpdateRemainingValues(newRemainingValues.ToImmutable());

                    if (!workList.Contains(existingState))
                        workList.Add(existingState);
                }

                return existingState;
            } else {
                uniqueState.Add(state, state);
                workList.Add(state);
                return state;
            }
        }

        var rewrittenCases = ArrayBuilder2<StateForCase>.GetInstance(casesForRootNode.Count);

        foreach (var state in casesForRootNode) {
            var rewrittenCase = state.RewriteNestedLengthTests();

            if (rewrittenCase.isImpossible)
                continue;

            rewrittenCases.Add(rewrittenCase);

            if (rewrittenCase.isFullyMatched)
                break;
        }

        var initialState = UniquifyState(new FrozenArrayBuilder<StateForCase>(rewrittenCases), []);

        while (workList.Count != 0) {
            var state = workList.RemoveLast();

            if (state.cases.Count == 0)
                continue;

            var first = state.cases[0];

            if (first.patternIsSatisfied) {
                if (first.isFullyMatched) {
                } else {
                    var stateWhenFails = state.cases.RemoveAt(0);
                    state.falseBranch = UniquifyState(stateWhenFails, state.remainingValues);
                }
            } else {
                switch (state.selectedTest = state.ComputeSelectedTest()) {
                    case BoundDagAssignmentEvaluation e when state.remainingValues.TryGetValue(e.input, out var currentValues):
                        if (state.remainingValues.TryGetValue(e.target, out var targetValues))
                            currentValues = currentValues.Intersect(targetValues);

                        state.trueBranch = UniquifyState(
                            RemoveEvaluation(state.cases, e),
                            state.remainingValues.SetItem(e.target, currentValues)
                        );

                        break;
                    case BoundDagEvaluation e:
                        state.trueBranch = UniquifyState(RemoveEvaluation(state.cases, e), state.remainingValues);
                        break;
                    case BoundDagTest d:
                        var foundExplicitNullTest = false;

                        SplitCases(
                            state,
                            d,
                            out var whenTrueDecisions,
                            out var whenTrueValues,
                            out var whenFalseDecisions,
                            out var whenFalseValues,
                            ref foundExplicitNullTest
                        );

                        state.trueBranch = UniquifyState(whenTrueDecisions, whenTrueValues);
                        state.falseBranch = UniquifyState(whenFalseDecisions, whenFalseValues);

                        if (foundExplicitNullTest && d is BoundDagNonNullTest { isExplicitTest: false } t)
                            state.selectedTest = new BoundDagNonNullTest(t.syntax, isExplicitTest: true, t.input, t.hasErrors);

                        break;
                    case var n:
                        throw ExceptionUtilities.UnexpectedValue(n.kind);
                }
            }
        }

        return new DecisionDag(initialState);
    }

    private void SplitCase(
        DagState state,
        StateForCase stateForCase,
        BoundDagTest test,
        IValueSet? whenTrueValues,
        IValueSet? whenFalseValues,
        out StateForCase whenTrue,
        out StateForCase whenFalse,
        ref bool foundExplicitNullTest) {
        stateForCase.remainingTests.Filter(
            this,
            test,
            state,
            whenTrueValues,
            whenFalseValues,
            out var whenTrueTests,
            out var whenFalseTests,
            ref foundExplicitNullTest
        );

        whenTrue = stateForCase.WithRemainingTests(whenTrueTests);
        whenFalse = stateForCase.WithRemainingTests(whenFalseTests);
    }

    private void SplitCases(
        DagState state,
        BoundDagTest test,
        out FrozenArrayBuilder<StateForCase> whenTrue,
        out ImmutableDictionary<BoundDagTemp, IValueSet> whenTrueValues,
        out FrozenArrayBuilder<StateForCase> whenFalse,
        out ImmutableDictionary<BoundDagTemp, IValueSet> whenFalseValues,
        ref bool foundExplicitNullTest) {
        var cases = state.cases;
        var whenTrueBuilder = ArrayBuilder2<StateForCase>.GetInstance(cases.Count);
        var whenFalseBuilder = ArrayBuilder2<StateForCase>.GetInstance(cases.Count);

        (whenTrueValues, whenFalseValues, var whenTruePossible, var whenFalsePossible) = SplitValues(state.remainingValues, test);
        whenTrueValues.TryGetValue(test.input, out var whenTrueValuesOpt);
        whenFalseValues.TryGetValue(test.input, out var whenFalseValuesOpt);

        foreach (var stateForCase in cases) {
            SplitCase(
                state,
                stateForCase,
                test,
                whenTrueValuesOpt,
                whenFalseValuesOpt,
                out var whenTrueState,
                out var whenFalseState,
                ref foundExplicitNullTest
            );

            if (whenTruePossible && !whenTrueState.isImpossible &&
                !(whenTrueBuilder.Any() && whenTrueBuilder.Last().isFullyMatched)) {
                whenTrueBuilder.Add(whenTrueState);
            }

            if (whenFalsePossible && !whenFalseState.isImpossible &&
                !(whenFalseBuilder.Any() && whenFalseBuilder.Last().isFullyMatched)) {
                whenFalseBuilder.Add(whenFalseState);
            }
        }

        whenTrue = AsFrozen(whenTrueBuilder);
        whenFalse = AsFrozen(whenFalseBuilder);
    }

    private static (
        ImmutableDictionary<BoundDagTemp, IValueSet> whenTrueValues,
        ImmutableDictionary<BoundDagTemp, IValueSet> whenFalseValues,
        bool truePossible,
        bool falsePossible)
        SplitValues(
        ImmutableDictionary<BoundDagTemp, IValueSet> values,
        BoundDagTest test) {
        switch (test) {
            case BoundDagEvaluation _:
            case BoundDagExplicitNullTest _:
            case BoundDagNonNullTest _:
                return (values, values, true, true);
            case BoundDagValueTest t:
                return ResultForRelation(BinaryOperatorKind.Equal, t.value);
            default:
                throw ExceptionUtilities.UnexpectedValue(test);
        }

        (
        ImmutableDictionary<BoundDagTemp, IValueSet> whenTrueValues,
        ImmutableDictionary<BoundDagTemp, IValueSet> whenFalseValues,
        bool truePossible,
        bool falsePossible)
        ResultForRelation(BinaryOperatorKind relation, ConstantValue value) {
            var input = test.input;
            var valueFac = ValueSetFactory.ForInput(input);

            if (valueFac is null)
                return (values, values, true, true);

            var fromTestPassing = valueFac.Related(relation.Operator(), value);
            var fromTestFailing = fromTestPassing.Complement();

            if (values.TryGetValue(input, out var tempValuesBeforeTest)) {
                fromTestPassing = fromTestPassing.Intersect(tempValuesBeforeTest);
                fromTestFailing = fromTestFailing.Intersect(tempValuesBeforeTest);
            }

            var whenTrueValues = values.SetItem(input, fromTestPassing);
            var whenFalseValues = values.SetItem(input, fromTestFailing);
            return (whenTrueValues, whenFalseValues, !fromTestPassing.isEmpty, !fromTestFailing.isEmpty);
        }
    }

    private static FrozenArrayBuilder<StateForCase> RemoveEvaluation(
        FrozenArrayBuilder<StateForCase> cases,
        BoundDagEvaluation e) {
        var builder = ArrayBuilder2<StateForCase>.GetInstance(cases.Count);

        foreach (var stateForCase in cases) {
            var remainingTests = stateForCase.remainingTests.RemoveEvaluation(e);

            if (remainingTests is Tests.False) {
            } else {
                builder.Add(new StateForCase(
                    index: stateForCase.index,
                    syntax: stateForCase.syntax,
                    remainingTests: remainingTests,
                    bindings: stateForCase.bindings,
                    caseLabel: stateForCase.caseLabel
                ));
            }
        }

        return AsFrozen(builder);
    }

    private void ComputeBoundDecisionDagNodes(DecisionDag decisionDag, BoundLeafDecisionDagNode defaultDecision) {
        var wasAcyclic = decisionDag.TryGetTopologicallySortedReachableStates(out var sortedStates);

        if (!wasAcyclic) {
            decisionDag.rootNode.dag = defaultDecision;
            return;
        }

        var uniqueNodes = PooledDictionary<BoundDecisionDagNode, BoundDecisionDagNode>.GetInstance();

        BoundDecisionDagNode UniqifyDagNode(BoundDecisionDagNode node) {
            return uniqueNodes.GetOrAdd(node, node);
        }

        _ = UniqifyDagNode(defaultDecision);

        for (var i = sortedStates.Length - 1; i >= 0; i--) {
            var state = sortedStates[i];

            if (state.cases.Count == 0) {
                state.dag = defaultDecision;
                continue;
            }

            var first = state.cases[0];

            if (first.patternIsSatisfied) {
                if (first.isFullyMatched) {
                    state.dag = FinalState(first.syntax, first.caseLabel, first.bindings);
                } else {
                    throw ExceptionUtilities.Unreachable();

                    // var whenTrue = FinalState(first.syntax, first.caseLabel, default);
                    // var whenFalse = state.falseBranch.dag;
                    // state.dag = UniqifyDagNode(new BoundWhenDecisionDagNode(first.syntax, first.bindings, first.whenClause, whenTrue, whenFalse));
                }

                BoundDecisionDagNode FinalState(SyntaxNode syntax, LabelSymbol label, ImmutableArray<BoundPatternBinding> bindings) {
                    var final = UniqifyDagNode(new BoundLeafDecisionDagNode(syntax, label));

                    if (bindings.IsDefaultOrEmpty)
                        return final;

                    throw ExceptionUtilities.Unreachable();
                    // return UniqifyDagNode(new BoundWhenDecisionDagNode(syntax, bindings, null, final, null));
                }
            } else {
                switch (state.selectedTest) {
                    case BoundDagEvaluation e: {
                            var next = state.trueBranch.dag;
                            state.dag = UniqifyDagNode(new BoundEvaluationDecisionDagNode(e.syntax, e, next));
                        }

                        break;
                    case BoundDagTest d: {
                            var whenTrue = state.trueBranch.dag;
                            var whenFalse = state.falseBranch.dag;
                            state.dag = UniqifyDagNode(new BoundTestDecisionDagNode(d.syntax, d, whenTrue, whenFalse));
                        }

                        break;
                    case var n:
                        throw ExceptionUtilities.UnexpectedValue(n?.kind);
                }
            }
        }

        uniqueNodes.Free();
    }

    private StateForCase MakeTestsForPattern(
        int index,
        SyntaxNode syntax,
        BoundDagTemp input,
        BoundPattern pattern,
        LabelSymbol label) {
        var tests = MakeAndSimplifyTestsAndBindings(input, pattern, out var bindings);
        return new StateForCase(index, syntax, tests, bindings, label);
    }

    private static FrozenArrayBuilder<T> AsFrozen<T>(ArrayBuilder2<T> builder) {
        return new FrozenArrayBuilder<T>(builder);
    }

    private Tests MakeAndSimplifyTestsAndBindings(
        BoundDagTemp input,
        BoundPattern pattern,
        out ImmutableArray<BoundPatternBinding> bindings) {
        var bindingsBuilder = ArrayBuilder<BoundPatternBinding>.GetInstance();
        var tests = MakeTestsAndBindings(input, pattern, bindingsBuilder);
        tests = SimplifyTestsAndBindings(tests, bindingsBuilder);
        bindings = bindingsBuilder.ToImmutableAndFree();
        return tests;
    }

    private static Tests SimplifyTestsAndBindings(
        Tests tests,
        ArrayBuilder<BoundPatternBinding> bindingsBuilder) {
        var usedValues = PooledHashSet<BoundDagEvaluation>.GetInstance();

        foreach (var binding in bindingsBuilder) {
            var temp = binding.tempContainingValue;

            if (temp.source is { })
                usedValues.Add(temp.source);
        }

        var result = ScanAndSimplify(tests);
        usedValues.Free();
        return result;

        Tests ScanAndSimplify(Tests tests) {
            switch (tests) {
                case Tests.SequenceTests seq:
                    var testSequence = seq.remainingTests;
                    var length = testSequence.Length;
                    var newSequence = ArrayBuilder<Tests>.GetInstance(length);
                    newSequence.AddRange(testSequence);

                    for (var i = length - 1; i >= 0; i--)
                        newSequence[i] = ScanAndSimplify(newSequence[i]);

                    return seq.Update(newSequence);
                case Tests.True _:
                case Tests.False _:
                    return tests;
                case Tests.One(BoundDagEvaluation e):
                    if (usedValues.Contains(e)) {
                        if (e.input.source is { })
                            usedValues.Add(e.input.source);

                        return tests;
                    } else {
                        return Tests.True.Instance;
                    }
                case Tests.One(BoundDagTest d):
                    if (d.input.source is { })
                        usedValues.Add(d.input.source);

                    return tests;
                case Tests.Not n:
                    return Tests.Not.Create(ScanAndSimplify(n.negated));
                default:
                    throw ExceptionUtilities.UnexpectedValue(tests);
            }
        }
    }

    private Tests MakeTestsAndBindings(
        BoundDagTemp input,
        BoundPattern pattern,
        ArrayBuilder<BoundPatternBinding> bindings) {
        return MakeTestsAndBindings(input, pattern, out _, bindings);
    }

    private Tests MakeTestsAndBindings(
        BoundDagTemp input,
        BoundPattern pattern,
        out BoundDagTemp output,
        ArrayBuilder<BoundPatternBinding> bindings) {
        switch (pattern) {
            case BoundConstantPattern constant:
                return MakeTestsForConstantPattern(input, constant, out output);
            case BoundDiscardPattern:
                output = input;
                return Tests.True.Instance;
            default:
                throw ExceptionUtilities.UnexpectedValue(pattern.kind);
        }
    }

    private Tests MakeTestsForConstantPattern(
        BoundDagTemp input,
        BoundConstantPattern constant,
        out BoundDagTemp output) {
        if (constant.constantValue == ConstantValue.Null) {
            output = input;
            return new Tests.One(new BoundDagExplicitNullTest(constant.syntax, input));
        } else {
            var tests = ArrayBuilder<Tests>.GetInstance(2);
            output = input = constant.value.type is { } type
                ? MakeConvertToType(input, constant.syntax, type, isExplicitTest: false, tests)
                : input;

            if (ValueSetFactory.ForInput(input)?.Related(BinaryOperatorKind.Equal, constant.constantValue).isEmpty == true) {
                tests.Add(Tests.False.Instance);
            } else {
                tests.Add(new Tests.One(new BoundDagValueTest(constant.syntax, constant.constantValue, input)));
            }

            return Tests.AndSequence.Create(tests);
        }
    }

    private BoundDagTemp OriginalInput(BoundDagTemp input, Symbol symbol) {
        return input;
    }

    private static BoundDagTemp OriginalInput(BoundDagTemp input) {
        return input;
    }

    private static void MakeCheckNotNull(
        BoundDagTemp input,
        SyntaxNode syntax,
        bool isExplicitTest,
        ArrayBuilder<Tests> tests) {
        if (input.type.IsNullableType())
            tests.Add(new Tests.One(new BoundDagNonNullTest(syntax, isExplicitTest, input)));
    }

    private BoundDagTemp MakeConvertToType(
        BoundDagTemp input,
        SyntaxNode syntax,
        TypeSymbol type,
        bool isExplicitTest,
        ArrayBuilder<Tests> tests) {
        MakeCheckNotNull(input, syntax, isExplicitTest, tests);

        if (!input.type.Equals(type, TypeCompareKind.AllIgnoreOptions)) {
            throw ExceptionUtilities.Unreachable();
            // var inputType = input.type.StrippedType();
            // var conversion = _conversions.ClassifyBuiltInConversion(inputType, type);

            // if (conversion.isImplicit) {
            // } else {
            //     tests.Add(new Tests.One(new BoundDagTypeTest(syntax, type, input)));
            // }

            // var evaluation = new BoundDagTypeEvaluation(syntax, type, input);
            // input = new BoundDagTemp(syntax, type, evaluation);
            // tests.Add(new Tests.One(evaluation));
        }

        return input;
    }

    private bool CheckInputRelation(
        SyntaxNode syntax,
        DagState state,
        BoundDagTest test,
        BoundDagTest other,
        out Tests relationCondition,
        out Tests relationEffect) {
        relationCondition = Tests.True.Instance;
        relationEffect = Tests.True.Instance;

        if (test.input == other.input)
            return true;

        if (test is not (BoundDagNonNullTest or BoundDagExplicitNullTest) &&
            other is not (BoundDagNonNullTest or BoundDagExplicitNullTest) &&
            !test.input.type.Equals(other.input.type, TypeCompareKind.AllIgnoreOptions)) {
            return false;
        }

        var s1Input = OriginalInput(test.input);
        var s2Input = OriginalInput(other.input);

        ArrayBuilder<Tests> conditions = null;

        while (s1Input.index == s2Input.index) {
            switch (s1Input.source, s2Input.source) {
                case var (s1, s2) when s1 == s2:
                    if (conditions is not null) {
                        relationCondition = Tests.AndSequence.Create(conditions);
                        relationEffect = new Tests.One(
                            new BoundDagAssignmentEvaluation(syntax, target: other.input, input: test.input)
                        );
                    }

                    return true;
                case (BoundDagEvaluation s1, BoundDagEvaluation s2) when s1.IsEquivalentTo(s2):
                    s1Input = OriginalInput(s1.input);
                    s2Input = OriginalInput(s2.input);
                    continue;
            }

            break;
        }

        conditions?.Free();
        return false;
    }

    private void CheckConsistentDecision(
        BoundDagTest test,
        BoundDagTest other,
        IValueSet? whenTrueValues,
        IValueSet? whenFalseValues,
        SyntaxNode syntax,
        out bool trueTestPermitsTrueOther,
        out bool falseTestPermitsTrueOther,
        out bool trueTestImpliesTrueOther,
        out bool falseTestImpliesTrueOther,
        ref bool foundExplicitNullTest) {
        trueTestPermitsTrueOther = true;
        falseTestPermitsTrueOther = true;
        trueTestImpliesTrueOther = false;
        falseTestImpliesTrueOther = false;

        switch (test) {
            case BoundDagNonNullTest _:
                switch (other) {
                    case BoundDagValueTest _:
                        falseTestPermitsTrueOther = false;
                        break;
                    case BoundDagExplicitNullTest _:
                        foundExplicitNullTest = true;
                        trueTestPermitsTrueOther = false;
                        falseTestImpliesTrueOther = true;
                        break;
                    case BoundDagNonNullTest n2:
                        if (n2.isExplicitTest)
                            foundExplicitNullTest = true;

                        trueTestImpliesTrueOther = true;
                        falseTestPermitsTrueOther = false;
                        break;
                    default:
                        falseTestPermitsTrueOther = false;
                        break;
                }

                break;
            case BoundDagValueTest _:
                switch (other) {
                    case BoundDagNonNullTest n2:
                        if (n2.isExplicitTest)
                            foundExplicitNullTest = true;

                        trueTestImpliesTrueOther = true;
                        break;
                    case BoundDagExplicitNullTest _:
                        foundExplicitNullTest = true;
                        trueTestPermitsTrueOther = false;
                        break;
                    case BoundDagValueTest v2:
                        HandleRelationWithValue(
                            BinaryOperatorKind.Equal,
                            v2.value,
                            out trueTestPermitsTrueOther,
                            out falseTestPermitsTrueOther,
                            out trueTestImpliesTrueOther,
                            out falseTestImpliesTrueOther
                        );

                        break;

                        void HandleRelationWithValue(
                            BinaryOperatorKind relation,
                            ConstantValue value,
                            out bool trueTestPermitsTrueOther,
                            out bool falseTestPermitsTrueOther,
                            out bool trueTestImpliesTrueOther,
                            out bool falseTestImpliesTrueOther) {
                            var sameTest = test.Equals(other);
                            trueTestPermitsTrueOther = whenTrueValues?.Any(relation, value) ?? true;
                            trueTestImpliesTrueOther = sameTest || trueTestPermitsTrueOther && (whenTrueValues?.All(relation, value) ?? false);
                            falseTestPermitsTrueOther = !sameTest && (whenFalseValues?.Any(relation, value) ?? true);
                            falseTestImpliesTrueOther = falseTestPermitsTrueOther && (whenFalseValues?.All(relation, value) ?? false);
                        }
                }

                break;
            case BoundDagExplicitNullTest _:
                foundExplicitNullTest = true;

                switch (other) {
                    case BoundDagNonNullTest n2:
                        if (n2.isExplicitTest)
                            foundExplicitNullTest = true;

                        trueTestPermitsTrueOther = false;
                        falseTestImpliesTrueOther = true;
                        break;
                    case BoundDagExplicitNullTest _:
                        foundExplicitNullTest = true;
                        trueTestImpliesTrueOther = true;
                        falseTestPermitsTrueOther = false;
                        break;
                    case BoundDagValueTest _:
                        trueTestPermitsTrueOther = false;
                        break;
                }

                break;
        }
    }
}
