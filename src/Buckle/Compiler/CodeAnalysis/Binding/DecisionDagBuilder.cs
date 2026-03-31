using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class DecisionDagBuilder {
    private static readonly ObjectPool<PooledDictionary<DagState, DagState>> UniqueStatePool =
        PooledDictionary<DagState, DagState>.CreatePool(DagStateEquivalence.Instance);

    private readonly Compilation _compilation;
    private readonly Conversions _conversions;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly LabelSymbol _defaultLabel;
    private readonly bool _forLowering;

    private DecisionDagBuilder(
        Compilation compilation,
        LabelSymbol defaultLabel,
        bool forLowering,
        BelteDiagnosticQueue diagnostics) {
        _compilation = compilation;
        _diagnostics = diagnostics;
        _defaultLabel = defaultLabel;
        _forLowering = forLowering;
    }

    internal static BoundDecisionDag CreateDecisionDagForSwitchStatement(
        Compilation compilation,
        SyntaxNode syntax,
        BoundExpression switchGoverningExpression,
        ImmutableArray<BoundSwitchSection> switchSections,
        LabelSymbol defaultLabel,
        BelteDiagnosticQueue diagnostics,
        bool forLowering = false) {
        var builder = new DecisionDagBuilder(compilation, defaultLabel, forLowering, diagnostics);
        return builder.CreateDecisionDagForSwitchStatement(syntax, switchGoverningExpression, switchSections);
    }

    private BoundDecisionDag CreateDecisionDagForSwitchStatement(
        SyntaxNode syntax,
        BoundExpression switchGoverningExpression,
        ImmutableArray<BoundSwitchSection> switchSections) {
        // var rootIdentifier = BoundDagTemp.ForOriginalInput(switchGoverningExpression);
        // var i = 0;
        // using var builder = TemporaryArray<StateForCase>.GetInstance(switchSections.Length);

        // foreach (var section in switchSections) {
        //     foreach (var label in section.switchLabels) {
        //         if (label.syntax.kind != SyntaxKind.DefaultSwitchLabel)
        //             builder.Add(MakeTestsForPattern(++i, label.syntax, rootIdentifier, label.pattern, label.label));
        //     }
        // }

        // return MakeBoundDecisionDag(syntax, ref builder.AsRef());
        return null;
    }

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

        // internal BoundDagTest ComputeSelectedTest() {
        //     return cases[0].remainingTests.ComputeSelectedTest();
        // }

        internal void UpdateRemainingValues(ImmutableDictionary<BoundDagTemp, IValueSet> newRemainingValues) {
            remainingValues = newRemainingValues;
            selectedTest = null;
            trueBranch = null;
            falseBranch = null;
        }
    }

    private readonly struct StateForCase {
        // public readonly int Index;
        // public readonly SyntaxNode Syntax;
        // public readonly Tests RemainingTests;
        // public readonly ImmutableArray<BoundPatternBinding> Bindings;
        // public readonly BoundExpression? WhenClause;
        // public readonly LabelSymbol CaseLabel;
        // public StateForCase(
        //     int Index,
        //     SyntaxNode Syntax,
        //     Tests RemainingTests,
        //     ImmutableArray<BoundPatternBinding> Bindings,
        //     BoundExpression? WhenClause,
        //     LabelSymbol CaseLabel) {
        //     this.Index = Index;
        //     this.Syntax = Syntax;
        //     this.RemainingTests = RemainingTests;
        //     this.Bindings = Bindings;
        //     this.WhenClause = WhenClause;
        //     this.CaseLabel = CaseLabel;
        // }

        // public bool isFullyMatched => RemainingTests is Tests.True && (WhenClause is null || WhenClause.constantValue == ConstantValue.True);

        // public bool patternIsSatisfied => RemainingTests is Tests.True;

        // public bool isImpossible => RemainingTests is Tests.False;

        // public override bool Equals(object? obj) {
        //     throw ExceptionUtilities.Unreachable();
        // }

        // public bool Equals(StateForCase other) {
        //     // We do not include Syntax, Bindings, WhereClause, or CaseLabel
        //     // because once the Index is the same, those must be the same too.
        //     return this.Index == other.Index &&
        //         this.RemainingTests.Equals(other.RemainingTests);
        // }

        // public override int GetHashCode() {
        //     return Hash.Combine(RemainingTests.GetHashCode(), Index);
        // }

        // public StateForCase WithRemainingTests(Tests newRemainingTests) {
        //     return newRemainingTests.Equals(RemainingTests)
        //         ? this
        //         : new StateForCase(Index, Syntax, newRemainingTests, Bindings, WhenClause, CaseLabel);
        // }

        // /// <inheritdoc cref="Tests.RewriteNestedLengthTests"/>
        // public StateForCase RewriteNestedLengthTests() {
        //     return this.WithRemainingTests(RemainingTests.RewriteNestedLengthTests());
        // }
    }


    private static FrozenArrayBuilder<T> AsFrozen<T>(ArrayBuilder2<T> builder) {
        return new FrozenArrayBuilder<T>(builder);
    }

    private readonly struct FrozenArrayBuilder<T> {
        private readonly ArrayBuilder2<T> _arrayBuilder;

        internal FrozenArrayBuilder(ArrayBuilder2<T> arrayBuilder) {
            if (arrayBuilder.Capacity >= ArrayBuilder2<T>.PooledArrayLengthLimitExclusive
                && arrayBuilder.Count < ArrayBuilder2<T>.PooledArrayLengthLimitExclusive
                && arrayBuilder.Capacity >= arrayBuilder.Count * 2) {
                arrayBuilder.Capacity = arrayBuilder.Count;
            }

            _arrayBuilder = arrayBuilder;
        }

        internal void Free()
            => _arrayBuilder.Free();

        public int Count => _arrayBuilder.Count;

        public T this[int i] => _arrayBuilder[i];

        public T First() => _arrayBuilder.First();

        public ArrayBuilder2<T>.Enumerator GetEnumerator() => _arrayBuilder.GetEnumerator();

        public FrozenArrayBuilder<T> RemoveAt(int index) {
            var builder = ArrayBuilder2<T>.GetInstance(Count - 1);

            for (var i = 0; i < index; i++)
                builder.Add(this[i]);

            for (int i = index + 1, n = Count; i < n; i++)
                builder.Add(this[i]);

            return AsFrozen(builder);
        }
    }

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
