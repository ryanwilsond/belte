using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.FlowAnalysis;

internal sealed partial class ControlFlowGraphBuilder {
    /// <summary>
    /// Builds BasicBlocks from BoundStatements.
    /// </summary>
    internal sealed class BasicBlockBuilder {
        private List<BasicBlock> _blocks = new List<BasicBlock>();
        private List<BoundStatement> _statements = new List<BoundStatement>();

        internal List<BasicBlock> Build(BoundBlockStatement block) {
            foreach (var statement in block.statements) {
                switch (statement.kind) {
                    case BoundNodeKind.LabelStatement:
                        StartBlock();
                        _statements.Add(statement);
                        break;
                    case BoundNodeKind.GotoStatement:
                    case BoundNodeKind.ConditionalGotoStatement:
                    case BoundNodeKind.ReturnStatement:
                        _statements.Add(statement);
                        StartBlock();
                        break;
                    case BoundNodeKind.NopStatement:
                    case BoundNodeKind.ExpressionStatement:
                    case BoundNodeKind.VariableDeclarationStatement:
                    case BoundNodeKind.TryStatement:
                        _statements.Add(statement);
                        break;
                    default:
                        throw new BelteInternalException($"Build: unexpected statement '{statement.kind}'");
                }
            }

            EndBlock();

            return _blocks.ToList();
        }

        private void EndBlock() {
            if (_statements.Any()) {
                var block = new BasicBlock();
                block.statements.AddRange(_statements);
                _blocks.Add(block);
                _statements.Clear();
            }
        }

        private void StartBlock() {
            EndBlock();
        }
    }
}
