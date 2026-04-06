using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDecisionDag {
    private ImmutableHashSet<LabelSymbol> _reachableLabels;
    private ImmutableArray<BoundDecisionDagNode> _topologicallySortedNodes;

    internal ImmutableHashSet<LabelSymbol> reachableLabels {
        get {
            if (_reachableLabels is null) {
                var result = ImmutableHashSet.CreateBuilder<LabelSymbol>(SymbolEqualityComparer.ConsiderEverything);
                foreach (var node in topologicallySortedNodes) {
                    if (node is BoundLeafDecisionDagNode leaf)
                        result.Add(leaf.label);
                }

                _reachableLabels = result.ToImmutableHashSet();
            }

            return _reachableLabels;
        }
    }

    internal ImmutableArray<BoundDecisionDagNode> topologicallySortedNodes {
        get {
            if (_topologicallySortedNodes.IsDefault)
                TopologicalSort.TryIterativeSort(rootNode, AddSuccessors, out _topologicallySortedNodes);

            return _topologicallySortedNodes;
        }
    }

    internal static void AddSuccessors(ref TemporaryArray<BoundDecisionDagNode> builder, BoundDecisionDagNode node) {
        switch (node) {
            case BoundEvaluationDecisionDagNode p:
                builder.Add(p.next);
                return;
            case BoundTestDecisionDagNode p:
                builder.Add(p.whenFalse);
                builder.Add(p.whenTrue);
                return;
            case BoundLeafDecisionDagNode d:
                return;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    internal BoundDecisionDag SimplifyDecisionDagIfConstantInput(BoundExpression input) {
        if (input.constantValue is null) {
            return this;
        } else {
            var inputConstant = input.constantValue;

            return Rewrite(MakeReplacement);

            BoundDecisionDagNode MakeReplacement(
                BoundDecisionDagNode dag,
                IReadOnlyDictionary<BoundDecisionDagNode, BoundDecisionDagNode> replacement) {
                if (dag is BoundTestDecisionDagNode p) {
                    switch (KnownResult(p.test)) {
                        case true:
                            return replacement[p.whenTrue];
                        case false:
                            return replacement[p.whenFalse];
                    }
                }

                return TrivialReplacement(dag, replacement);
            }

            bool? KnownResult(BoundDagTest choice) {
                if (!choice.input.isOriginalInput) {
                    return null;
                }

                switch (choice) {
                    case BoundDagExplicitNullTest d:
                        return ConstantValue.IsNull(inputConstant);
                    case BoundDagNonNullTest d:
                        return !ConstantValue.IsNull(inputConstant);
                    case BoundDagValueTest d:
                        return d.value == inputConstant;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(choice);
                }
            }
        }
    }

    internal bool ContainsAnySynthesizedNodes() {
        return topologicallySortedNodes.Any(
            static node => node is BoundEvaluationDecisionDagNode e &&
                e.evaluation.kind == BoundKind.DagAssignmentEvaluation
        );
    }

    internal BoundDecisionDag Rewrite(
        Func<BoundDecisionDagNode, IReadOnlyDictionary<BoundDecisionDagNode, BoundDecisionDagNode>, BoundDecisionDagNode> makeReplacement) {
        var sortedNodes = topologicallySortedNodes;
        var replacement = PooledDictionary<BoundDecisionDagNode, BoundDecisionDagNode>.GetInstance();

        for (var i = sortedNodes.Length - 1; i >= 0; i--) {
            var node = sortedNodes[i];
            var newNode = makeReplacement(node, replacement);
            replacement.Add(node, newNode);
        }

        var newRoot = replacement[rootNode];
        replacement.Free();
        return Update(newRoot);
    }

    internal static BoundDecisionDagNode TrivialReplacement(
        BoundDecisionDagNode dag,
        IReadOnlyDictionary<BoundDecisionDagNode, BoundDecisionDagNode> replacement) {
        switch (dag) {
            case BoundEvaluationDecisionDagNode p:
                return p.Update(p.evaluation, replacement[p.next]);
            case BoundTestDecisionDagNode p:
                return p.Update(p.test, replacement[p.whenTrue], replacement[p.whenFalse]);
            case BoundLeafDecisionDagNode p:
                return p;
            default:
                throw ExceptionUtilities.UnexpectedValue(dag);
        }
    }
}
