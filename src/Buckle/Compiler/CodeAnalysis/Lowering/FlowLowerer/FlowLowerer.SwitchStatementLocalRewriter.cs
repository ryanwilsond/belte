using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private sealed class SwitchStatementLocalRewriter : BaseSwitchLocalRewriter {
        private readonly Dictionary<SyntaxNode, LabelSymbol> _sectionLabels
            = PooledDictionary<SyntaxNode, LabelSymbol>.GetInstance();

        public static BoundStatement Rewrite(FlowLowerer flowLowerer, BoundSwitchStatement node) {
            var rewriter = new SwitchStatementLocalRewriter(node, flowLowerer);
            var result = rewriter.LowerSwitchStatement(node);
            rewriter.Free();
            return result;
        }

        private protected override LabelSymbol GetDagNodeLabel(BoundDecisionDagNode dag) {
            var result = base.GetDagNodeLabel(dag);

            if (dag is BoundLeafDecisionDagNode d) {
                var section = d.syntax.parent;

                if (section.kind == SyntaxKind.SwitchSection) {
                    if (_sectionLabels.TryGetValue(section, out var replacementLabel))
                        return replacementLabel;

                    _sectionLabels.Add(section, result);
                }
            }

            return result;
        }

        private SwitchStatementLocalRewriter(BoundSwitchStatement node, FlowLowerer flowLowerer)
            : base(node.syntax, flowLowerer, node.switchSections.SelectAsArray(section => section.syntax),
                  generateInstrumentation: true) {
        }

        private BoundStatement LowerSwitchStatement(BoundSwitchStatement node) {
            var result = ArrayBuilder<BoundStatement>.GetInstance();
            var outerVariables = ArrayBuilder<DataContainerSymbol>.GetInstance();
            var loweredSwitchGoverningExpression = (BoundExpression)_flowLowerer.Visit(node.expression);

            // var instrumentedExpression = _localRewriter.Instrumenter.InstrumentSwitchStatementExpression(node, loweredSwitchGoverningExpression, _factory);
            if (loweredSwitchGoverningExpression.constantValue is null) {
                // loweredSwitchGoverningExpression = instrumentedExpression;
            } else {
                // result.Add(new BoundExpressionStatement(node.syntax, instrumentedExpression));
                result.Add(new BoundExpressionStatement(node.syntax, loweredSwitchGoverningExpression));
            }

            outerVariables.AddRange(node.innerLocals);

            var decisionDag = ShareTempsIfPossibleAndEvaluateInput(
                node.GetDecisionDagForLowering(),
                loweredSwitchGoverningExpression,
                result,
                out _
            );

            if (_generateInstrumentation) {
                if (result.Count == 0)
                    result.Add(BoundFactory.Nop());

                result.Add(BoundSequencePoint.CreateHidden());
            }

            (var loweredDag, var switchSections) = LowerDecisionDag(decisionDag);

            var temps = _tempAllocator.AllTemps();
            outerVariables.AddRange(temps);

            foreach (var temp in temps) {
                // TODO This never stays, we are only doing this for the declaration trigger in the code generator
                // Meaning there is probably a better way to mark the declaration without emitting a useless instruction
                result.Add(new BoundLocalDeclarationStatement(node.syntax,
                    new BoundDataContainerDeclaration(node.syntax,
                        temp,
                        BoundFactory.Literal(node.syntax,
                            temp.type.IsNullableType() ? null : LiteralUtilities.GetDefaultValue(temp.type.EnumUnderlyingTypeOrSelf().specialType),
                            temp.type
                        )
                    ))
                );
            }

            // if (_whenNodeIdentifierLocal is not null)
            //     outerVariables.Add(_whenNodeIdentifierLocal);

            result.Add(BoundFactory.Block(node.syntax, loweredDag.ToArray()));

            foreach (var section in node.switchSections) {
                var sectionBuilder = ArrayBuilder<BoundStatement>.GetInstance();
                sectionBuilder.AddRange(switchSections[section.syntax]);

                foreach (var switchLabel in section.switchLabels)
                    sectionBuilder.Add(new BoundLabelStatement(section.syntax, switchLabel.label));

                sectionBuilder.AddRange(_flowLowerer.VisitList(section.statements));

                var statements = sectionBuilder.ToImmutableAndFree();

                // if (section.locals.IsEmpty) {
                //     result.Add(_factory.StatementList(statements));
                // } else {
                //     outerVariables.AddRange(section.locals);

                //     result.Add(new BoundScope(section.Syntax, section.Locals, statements));
                // }
                // TODO Is this the same as above?
                result.Add(new BoundBlockStatement(section.syntax, statements, section.locals, []));
            }

            if (_generateInstrumentation)
                result.Add(BoundSequencePoint.CreateHidden());

            result.Add(new BoundLabelStatement(node.syntax, node.breakLabel));
            BoundStatement translatedSwitch = new BoundBlockStatement(
                node.syntax,
                result.ToImmutableAndFree(),
                outerVariables.ToImmutableAndFree(),
                node.innerLocalFunctions
            );

            // if (_generateInstrumentation)
            //     translatedSwitch = _localRewriter.Instrumenter.InstrumentSwitchStatement(node, translatedSwitch);

            return translatedSwitch;
        }
    }
}
