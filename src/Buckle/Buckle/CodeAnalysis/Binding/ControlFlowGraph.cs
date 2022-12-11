using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle.IO;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Creates a graphical control flow graph from a bound block.
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
    /// Creates a control flow graph from a block.
    /// </summary>
    /// <param name="body">Block to create from</param>
    /// <returns>Control flow graph</returns>
    internal static ControlFlowGraph Create(BoundBlockStatement body) {
        var basicBlockBuilder = new BasicBlockBuilder();
        var blocks = basicBlockBuilder.Build(body);

        var graphBuilder = new GraphBuilder();
        return graphBuilder.Build(blocks);
    }

    /// <summary>
    /// Checks (using a control flow graph) if all code paths in a body return.
    /// </summary>
    /// <param name="body">Body to check</param>
    /// <returns>If all code paths return</returns>
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
    /// Writes control flow graph to out.
    /// </summary>
    /// <param name="writer">Out</param>
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
    /// Block in the graph, represents a statement.
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
    /// Branch in the graph, represents code continuing from one statement to another.
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
        private List<BasicBlock> blocks_ = new List<BasicBlock>();
        private List<BoundStatement> statements_ = new List<BoundStatement>();

        internal List<BasicBlock> Build(BoundBlockStatement block) {
            foreach (var statement in block.statements) {
                switch (statement.type) {
                    case BoundNodeType.LabelStatement:
                        StartBlock();
                        statements_.Add(statement);
                        break;
                    case BoundNodeType.GotoStatement:
                    case BoundNodeType.ConditionalGotoStatement:
                    case BoundNodeType.ReturnStatement:
                        statements_.Add(statement);
                        StartBlock();
                        break;
                    case BoundNodeType.NopStatement:
                    case BoundNodeType.ExpressionStatement:
                    case BoundNodeType.VariableDeclarationStatement:
                    case BoundNodeType.TryStatement:
                        statements_.Add(statement);
                        break;
                    default:
                        throw new Exception($"Build: unexpected statement '{statement.type}'");
                }
            }

            EndBlock();
            return blocks_.ToList();
        }

        private void EndBlock() {
            if (statements_.Any()) {
                var block = new BasicBlock();
                block.statements.AddRange(statements_);
                blocks_.Add(block);
                statements_.Clear();
            }
        }

        private void StartBlock() {
            EndBlock();
        }
    }

    /// <summary>
    /// Builds a graph from graph blocks and branches.
    /// </summary>
    internal sealed class GraphBuilder {
        private Dictionary<BoundStatement, BasicBlock> blockFromStatement_ =
            new Dictionary<BoundStatement, BasicBlock>();
        private Dictionary<BoundLabel, BasicBlock> blockFromLabel_ = new Dictionary<BoundLabel, BasicBlock>();
        private List<BasicBlockBranch> branches_ = new List<BasicBlockBranch>();
        private BasicBlock start_ = new BasicBlock(true);
        private BasicBlock end_ = new BasicBlock(false);

        internal ControlFlowGraph Build(List<BasicBlock> blocks) {
            var basicBlockBuilder = new BasicBlockBuilder();

            if (!blocks.Any())
                Connect(start_, end_);
            else
                Connect(start_, blocks.First());

            foreach (var block in blocks) {
                foreach (var statement in block.statements) {
                    blockFromStatement_.Add(statement, block);

                    if (statement is BoundLabelStatement labelStatement)
                        blockFromLabel_.Add(labelStatement.label, block);
                }
            }

            for (int i=0; i<blocks.Count; i++) {
                var current = blocks[i];
                var next = i == blocks.Count - 1 ? end_ : blocks[i+1];

                foreach (var statement in current.statements) {
                    var isLastStatement = statement == current.statements.Last();

                    switch (statement.type) {
                        case BoundNodeType.GotoStatement:
                            var gs = (BoundGotoStatement)statement;
                            var toBlock = blockFromLabel_[gs.label];
                            Connect(current, toBlock);
                            break;
                        case BoundNodeType.ConditionalGotoStatement:
                            var cgs = (BoundConditionalGotoStatement)statement;
                            var thenBlock = blockFromLabel_[cgs.label];
                            var elseBlock = next;
                            var negatedCondition = Negate(cgs.condition);
                            var thenCondition = cgs.jumpIfTrue ? cgs.condition : negatedCondition;
                            var elseCondition = cgs.jumpIfTrue ? negatedCondition : cgs.condition;

                            Connect(current, thenBlock, thenCondition);
                            Connect(current, elseBlock, elseCondition);
                            break;
                        case BoundNodeType.ReturnStatement:
                            Connect(current, end_);
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

            blocks.Insert(0, start_);
            blocks.Add(end_);

            return new ControlFlowGraph(start_, end_, blocks, branches_);
        }

        private void RemoveBlock(List<BasicBlock> blocks, BasicBlock block) {
            blocks.Remove(block);

            foreach (var branch in block.incoming) {
                branch.from.outgoing.Remove(branch);
                branches_.Remove(branch);
            }

            foreach (var branch in block.outgoing) {
                branch.to.incoming.Remove(branch);
                branches_.Remove(branch);
            }
        }

        private BoundExpression Negate(BoundExpression condition) {
            if (condition is BoundLiteralExpression literal) {
                var value = (bool)literal.value;
                return new BoundLiteralExpression(!value);
            }

            var op = BoundUnaryOperator.Bind(SyntaxType.EXCLAMATION_TOKEN, new BoundTypeClause(TypeSymbol.Bool));
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
            branches_.Add(branch);
        }
    }
}
