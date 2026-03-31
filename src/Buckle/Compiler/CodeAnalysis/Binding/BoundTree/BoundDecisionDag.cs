using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDecisionDag {
    private ImmutableHashSet<LabelSymbol> _reachableLabels;
    private ImmutableArray<BoundDecisionDagNode> _topologicallySortedNodes;

    internal ImmutableHashSet<LabelSymbol> reachableLabels {
        get {
            if (_reachableLabels is null) {
                var result = ImmutableHashSet.CreateBuilder<LabelSymbol>(SymbolEqualityComparer.ConsiderEverything);
                foreach (var node in topologicallySortedNodes) {
                    // if (node is BoundLeafDecisionDagNode leaf) {
                    //     result.Add(leaf.Label);
                    // }
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
            // case BoundEvaluationDecisionDagNode p:
            //     builder.Add(p.Next);
            //     return;
            // case BoundTestDecisionDagNode p:
            //     builder.Add(p.WhenFalse);
            //     builder.Add(p.WhenTrue);
            //     return;
            // case BoundLeafDecisionDagNode d:
            //     return;
            // case BoundWhenDecisionDagNode w:
            //     builder.Add(w.WhenTrue);
            //     builder.AddIfNotNull(w.WhenFalse);
            //     return;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    internal BoundDecisionDag SimplifyDecisionDagIfConstantInput(BoundExpression input) {
        // if (input.constantValue is null) {
        //     return this;
        // } else {
        //     var inputConstant = input.constantValue;

        //     return Rewrite(makeReplacement);

        //     BoundDecisionDagNode MakeReplacement(
        //         BoundDecisionDagNode dag,
        //         IReadOnlyDictionary<BoundDecisionDagNode, BoundDecisionDagNode> replacement) {
        //         if (dag is BoundTestDecisionDagNode p) {
        //             // This is the key to the optimization. The result of a top-level test might be known if the input is constant.
        //             switch (knownResult(p.Test)) {
        //                 case true:
        //                     return replacement[p.WhenTrue];
        //                 case false:
        //                     return replacement[p.WhenFalse];
        //             }
        //         }

        //         return TrivialReplacement(dag, replacement);
        //     }

        //     // Is the decision's result known because the input is a constant?
        //     bool? knownResult(BoundDagTest choice) {
        //         if (!choice.Input.IsOriginalInput) {
        //             // This is a test of something other than the main input; result unknown
        //             return null;
        //         }

        //         switch (choice) {
        //             case BoundDagExplicitNullTest d:
        //                 return inputConstant.IsNull;
        //             case BoundDagNonNullTest d:
        //                 return !inputConstant.IsNull;
        //             case BoundDagValueTest d:
        //                 return d.Value == inputConstant;
        //             case BoundDagTypeTest d:
        //                 return inputConstant.IsNull ? (bool?)false : null;
        //             case BoundDagRelationalTest d:
        //                 var f = ValueSetFactory.ForType(input.Type);
        //                 if (f is null) return null;
        //                 return f.Related(d.Relation.Operator(), inputConstant, d.Value);
        //             default:
        //                 throw ExceptionUtilities.UnexpectedValue(choice);
        //         }
        //     }
        // }
        return null;
    }
}
