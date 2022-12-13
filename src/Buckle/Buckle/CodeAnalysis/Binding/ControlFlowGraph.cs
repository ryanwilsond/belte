using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle.IO;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Creates a graphical control flow graph from a <see cref="BasicBlock" />.
/// </summary>
internal sealed class ControlFlowGraph {
    private ControlFlowGraph(
        BasicBlock start, BasicBlock end, List<BasicBlock> blocks, List<BasicBlockBranch> branch) {
        this.start = start;
        this.end = end;
        this.blocks = blocks;
        branches = branch;
    }

    /// <summary>
    /// Start graph block.
    /// </summary>
    internal BasicBlock start { get; }

    /// <summary>
    /// End graph block.
    /// </summary>
    internal BasicBlock end { get; }

    /// <summary>
    /// All blocks in the graph.
    /// </summary>
    internal List<BasicBlock> blocks { get; }

    /// <summary>
    /// All branches in the graph.
    /// </summary>
    /// <value></value>
    internal List<BasicBlockBranch> branches { get; }

    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" /> from a <see cref="BoundBlockStatement" />.
    /// </summary>
    /// <param name="body">Block to create from.</param>
    /// <returns>Control flow graph.</returns>
    internal static ControlFlowGraph Create(BoundBlockStatement body) {
        var basicBlockBuilder = new BasicBlockBuilder();
        var blocks = basicBlockBuilder.Build(body);

        var graphBuilder = new GraphBuilder();
        return graphBuilder.Build(blocks);
    }

    /// <summary>
    /// Checks (using a <see cref="ControlFlowGraph" />) if all code paths in a body return.
    /// </summary>
    /// <param name="body">Body to check.</param>
    /// <returns>If all code paths return.</returns>
    internal static bool AllPathsReturn(BoundBlockStatement body) {
        var graph = Create(body);

        foreach (var branch in graph.end.incoming) {
            var lastStatement = branch.from.statements.LastOrDefault();

            if (lastStatement == null || lastStatement.type != BoundNodeType.ReturnStatement)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Writes <see cref="ControlFlowGraph" /> to out.
    /// </summary>
    /// <param name="writer">Out.</param>
    internal void WriteTo(TextWriter writer) {
        string Quote(string text) {
            return "\"" + text.TrimEnd()
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\l") + "\"";
        }

        writer.WriteLine("digraph G {");

        var blockIds = new Dictionary<BasicBlock, string>();

        for (int i=0; i<blocks.Count; i++) {
            var id = $"N{i}";
            blockIds.Add(blocks[i], id);
        }

        foreach (var block in blocks) {
            var id = blockIds[block];
            var label = Quote(block.ToString());
            writer.WriteLine($"    {id} [label = {label}, shape = box]");
        }

        foreach (var branch in branches) {
            var fromId = blockIds[branch.from];
            var toId = blockIds[branch.to];
            var label = Quote(branch.ToString());
            writer.WriteLine($"    {fromId} -> {toId} [label = {label}]");
        }

        writer.WriteLine("}");
    }

    /// <summary>
    /// Block in the graph, represents a <see cref="BoundStatement" />.
    /// </summary>
    internal sealed class BasicBlock {
        internal List<BoundStatement> statements { get; } = new List<BoundStatement>();
        internal List<BasicBlockBranch> incoming { get; } = new List<BasicBlockBranch>();
        internal List<BasicBlockBranch> outgoing { get; } = new List<BasicBlockBranch>();
        internal bool isStart { get; }
        internal bool isEnd { get; }

        internal BasicBlock() {}

        internal BasicBlock(bool isStart) {
            this.isStart = isStart;
            isEnd = !isStart;
        }

        public override string ToString() {
            if (isStart)
                return "<Start>";
            if (isEnd)
                return "<End>";

            using (var writer = new StringWriter()) {
                foreach (var statement in statements)
                    statement.WriteTo(writer);

                return writer.ToString();
            }
        }
    }

    /// <summary>
    /// Branch in the graph, represents code continuing from one <see cref="BoundStatement" /> to another.
    /// </summary>
    internal sealed class BasicBlockBranch {
        internal BasicBlock from { get; }
        internal BasicBlock to { get; }
        internal BoundExpression condition { get; }

        internal BasicBlockBranch(BasicBlock from, BasicBlock to, BoundExpression condition) {
            this.from = from;
            this.to = to;
            this.condition = condition;
        }

        public override string ToString() {
            if (condition == null)
                return string.Empty;

            return condition.ToString();
        }
    }

    /// <summary>
    /// Builds blocks from statements.
    /// </summary>
    internal sealed class BasicBlockBuilder {
        private List<BasicBlock> _blocks = new List<BasicBlock>();
        private List<BoundStatement> _statements = new List<BoundStatement>();

        internal List<BasicBlock> Build(BoundBlockStatement block) {
            foreach (var statement in block.statements) {
                switch (statement.type) {
                    case BoundNodeType.LabelStatement:
                        StartBlock();
                        _statements.Add(statement);
                        break;
                    case BoundNodeType.GotoStatement:
                    case BoundNodeType.ConditionalGotoStatement:
                    case BoundNodeType.ReturnStatement:
                        _statements.Add(statement);
                        StartBlock();
                        break;
                    case BoundNodeType.NopStatement:
                    case BoundNodeType.ExpressionStatement:
                    case BoundNodeType.VariableDeclarationStatement:
                    case BoundNodeType.TryStatement:
                        _statements.Add(statement);
                        break;
                    default:
                        throw new Exception($"Build: unexpected statement '{statement.type}'");
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

    /// <summary>
    /// Builds a <see cref="ControlFlowGraph" /> from graph blocks and branches.
    /// </summary>
    internal sealed class GraphBuilder {
        private Dictionary<BoundStatement, BasicBlock> _blockFromStatement =
            new Dictionary<BoundStatement, BasicBlock>();
        private Dictionary<BoundLabel, BasicBlock> _blockFromLabel = new Dictionary<BoundLabel, BasicBlock>();
        private List<BasicBlockBranch> _branches = new List<BasicBlockBranch>();
        private BasicBlock _start = new BasicBlock(true);
        private BasicBlock _end = new BasicBlock(false);

        internal ControlFlowGraph Build(List<BasicBlock> blocks) {
            var basicBlockBuilder = new BasicBlockBuilder();

            if (!blocks.Any())
                Connect(_start, _end);
            else
                Connect(_start, blocks.First());

            foreach (var block in blocks) {
                foreach (var statement in block.statements) {
                    _blockFromStatement.Add(statement, block);

                    if (statement is BoundLabelStatement labelStatement)
                        _blockFromLabel.Add(labelStatement.label, block);
                }
            }

            for (int i=0; i<blocks.Count; i++) {
                var current = blocks[i];
                var next = i == blocks.Count - 1 ? _end : blocks[i+1];

                foreach (var statement in current.statements) {
                    var isLastStatement = statement == current.statements.Last();

                    switch (statement.type) {
                        case BoundNodeType.GotoStatement:
                            var gs = (BoundGotoStatement)statement;
                            var toBlock = _blockFromLabel[gs.label];
                            Connect(current, toBlock);
                            break;
                        case BoundNodeType.ConditionalGotoStatement:
                            var cgs = (BoundConditionalGotoStatement)statement;
                            var thenBlock = _blockFromLabel[cgs.label];
                            var elseBlock = next;
                            var negatedCondition = Negate(cgs.condition);
                            var thenCondition = cgs.jumpIfTrue ? cgs.condition : negatedCondition;
                            var elseCondition = cgs.jumpIfTrue ? negatedCondition : cgs.condition;

                            Connect(current, thenBlock, thenCondition);
                            Connect(current, elseBlock, elseCondition);
                            break;
                        case BoundNodeType.ReturnStatement:
                            Connect(current, _end);
                            break;
                        case BoundNodeType.NopStatement:
                        case BoundNodeType.ExpressionStatement:
                        case BoundNodeType.VariableDeclarationStatement:
                        case BoundNodeType.TryStatement:
                        case BoundNodeType.LabelStatement:
                            if (isLastStatement)
                                Connect(current, next);
                            break;
                        default:
                            throw new Exception($"Build: unexpected statement '{statement.type}'");
                    }
                }
            }

            void Scan() {
                foreach (var block in blocks) {
                    if (!block.incoming.Any()) {
                        RemoveBlock(blocks, block);
                        Scan();
                        return;
                    }
                }
            }

            Scan();

            blocks.Insert(0, _start);
            blocks.Add(_end);

            return new ControlFlowGraph(_start, _end, blocks, _branches);
        }

        private void RemoveBlock(List<BasicBlock> blocks, BasicBlock block) {
            blocks.Remove(block);

            foreach (var branch in block.incoming) {
                branch.from.outgoing.Remove(branch);
                _branches.Remove(branch);
            }

            foreach (var branch in block.outgoing) {
                branch.to.incoming.Remove(branch);
                _branches.Remove(branch);
            }
        }

        private BoundExpression Negate(BoundExpression condition) {
            if (condition is BoundLiteralExpression literal) {
                var value = (bool)literal.value;
                return new BoundLiteralExpression(!value);
            }

            var op = BoundUnaryOperator.Bind(SyntaxType.ExclamationToken, new BoundTypeClause(TypeSymbol.Bool));
            return new BoundUnaryExpression(op, condition);
        }

        private void Connect(BasicBlock from, BasicBlock to, BoundExpression condition = null) {
            if (condition is BoundLiteralExpression l) {
                var value = (bool)l.value;

                if (value)
                    condition = null;
                else
                    return;
            }

            var branch = new BasicBlockBranch(from, to, condition);
            from.outgoing.Add(branch);
            to.incoming.Add(branch);
            _branches.Add(branch);
        }
    }
}
