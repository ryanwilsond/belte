using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract class BaseSwitchLocalRewriter : DecisionDagRewriter {
        private readonly PooledDictionary<SyntaxNode, ArrayBuilder<BoundStatement>> _switchArms
            = PooledDictionary<SyntaxNode, ArrayBuilder<BoundStatement>>.GetInstance();

        private protected override ArrayBuilder<BoundStatement> BuilderForSection(SyntaxNode whenClauseSyntax) {
            var sectionSyntax = whenClauseSyntax is SwitchLabelSyntax l ? l.parent : whenClauseSyntax;
            var found = _switchArms.TryGetValue(sectionSyntax, out var result);

            if (!found || result is null)
                throw new InvalidOperationException();

            return result;
        }

        private protected BaseSwitchLocalRewriter(
            SyntaxNode node,
            FlowLowerer flowLowerer,
            ImmutableArray<SyntaxNode> arms,
            bool generateInstrumentation)
            : base(node, flowLowerer, generateInstrumentation) {
            foreach (var arm in arms) {
                var armBuilder = ArrayBuilder<BoundStatement>.GetInstance();

                if (_generateInstrumentation)
                    armBuilder.Add(BoundSequencePoint.CreateHidden());

                _switchArms.Add(arm, armBuilder);
            }
        }

        private protected new void Free() {
            _switchArms.Free();
            base.Free();
        }

        private protected (ImmutableArray<BoundStatement> loweredDag, ImmutableDictionary<SyntaxNode, ImmutableArray<BoundStatement>> switchSections)
            LowerDecisionDag(BoundDecisionDag decisionDag) {
            var loweredDag = LowerDecisionDagCore(decisionDag);
            var switchSections = _switchArms.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutableAndFree());
            _switchArms.Clear();
            return (loweredDag, switchSections);
        }
    }
}
