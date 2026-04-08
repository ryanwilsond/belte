using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.FlowAnalysis;

internal sealed partial class ControlFlowGraphBuilder {
    /// <summary>
    /// Builds BasicBlocks from BoundStatements.
    /// </summary>
    internal sealed class BasicBlockBuilder {
        internal readonly List<TryRegion> regions = [];

        private readonly List<BasicBlock> _blocks = [];
        private readonly List<BoundStatement> _statements = [];
        private BasicBlock _currentBlock;

        internal List<BasicBlock> Build(BoundBlockStatement block) {
            _currentBlock = new BasicBlock();
            VisitBlock(block);
            EndBlock();
            return _blocks.ToList();
        }

        private void VisitBlock(BoundBlockStatement block) {
            foreach (var statement in block.statements) {
                var node = statement;
again:
                switch (node.kind) {
                    case BoundKind.LabelStatement:
                        StartBlock();
                        _statements.Add(node);
                        break;
                    case BoundKind.GotoStatement:
                    case BoundKind.ConditionalGotoStatement:
                    case BoundKind.ReturnStatement:
                    case BoundKind.ExpressionStatement
                        when (node as BoundExpressionStatement).expression is BoundThrowExpression:
                        _statements.Add(node);
                        StartBlock();
                        break;
                    case BoundKind.NopStatement:
                    case BoundKind.ExpressionStatement:
                    case BoundKind.SwitchDispatch:
                    case BoundKind.InlineILStatement:
                    case BoundKind.LocalDeclarationStatement:
                    case BoundKind.LocalFunctionStatement:
                        _statements.Add(node);
                        break;
                    case BoundKind.TryStatement:
                        BuildTryRegion((BoundTryStatement)node);
                        break;
                    case BoundKind.SequencePoint:
                        var inner = ((BoundSequencePoint)node).statement;

                        if (inner is null)
                            break;

                        node = inner;
                        goto again;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(node.kind);
                }
            }
        }

        private void BuildTryRegion(BoundTryStatement node) {
            StartBlock();

            var tryStartBlock = _currentBlock;

            VisitBlock((BoundBlockStatement)node.body);

            StartBlock();

            var tryRegion = new TryRegion() {
                tryStart = tryStartBlock
            };

            if (node.catchBody is not null) {
                tryRegion.catchBlock = _currentBlock;
                VisitBlock((BoundBlockStatement)node.catchBody);
                StartBlock();
            }

            if (node.finallyBody is not null) {
                tryRegion.finallyBlock = _currentBlock;
                VisitBlock((BoundBlockStatement)node.finallyBody);
                StartBlock();
            }

            tryRegion.tryEnd = _currentBlock;
            // Needs to be somewhere between the end of the finally and the end of the program if the finally is the last statement
            _statements.Add(new BoundNopStatement(node.syntax));

            StartBlock();

            regions.Add(tryRegion);
        }

        private void EndBlock() {
            if (_statements.Count > 0) {
                _currentBlock.statements.AddRange(_statements);
                _blocks.Add(_currentBlock);
                _statements.Clear();
                _currentBlock = new BasicBlock();
            }
        }

        private void StartBlock() {
            EndBlock();
        }
    }
}
