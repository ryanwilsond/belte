using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.IO;

namespace Buckle.CodeAnalysis.Binding {
    internal sealed class ControlFlowGraph {
        public BasicBlock start { get; }
        public BasicBlock end { get; }
        public List<BasicBlock> blocks { get; }
        public List<BasicBlockBranch> branches { get; }

        private ControlFlowGraph(
            BasicBlock start_, BasicBlock end_, List<BasicBlock> blocks_, List<BasicBlockBranch> branch_) {
            start = start_;
            end = end_;
            blocks = blocks_;
            branches = branch_;
        }

        public sealed class BasicBlock {
            public List<BoundStatement> statements { get; } = new List<BoundStatement>();
            public List<BasicBlockBranch> incoming { get; } = new List<BasicBlockBranch>();
            public List<BasicBlockBranch> outgoing { get; } = new List<BasicBlockBranch>();
            public bool isStart { get; }
            public bool isEnd { get; }

            public BasicBlock() {}

            public BasicBlock(bool isStart_) {
                isStart = isStart_;
                isEnd = !isStart_;
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

        public sealed class BasicBlockBranch {
            public BasicBlock from { get; }
            public BasicBlock to { get; }
            public BoundExpression condition { get; }

            public BasicBlockBranch(BasicBlock from_, BasicBlock to_, BoundExpression condition_) {
                from = from_;
                to = to_;
                condition = condition_;
            }

            public override string ToString() {
                if (condition == null)
                    return string.Empty;

                return condition.ToString();
            }
        }

        public sealed class BasicBlockBuilder {
            private List<BasicBlock> blocks_ = new List<BasicBlock>();
            private List<BoundStatement> statements_ = new List<BoundStatement>();

            public List<BasicBlock> Build(BoundBlockStatement block) {
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
                            statements_.Add(statement);
                            break;
                        default:
                            throw new Exception($"unexpected statement: {statement.type}");
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

        public sealed class GraphBuilder {
            private Dictionary<BoundStatement, BasicBlock> blockFromStatement_ =
                new Dictionary<BoundStatement, BasicBlock>();
            private Dictionary<BoundLabel, BasicBlock> blockFromLabel_ = new Dictionary<BoundLabel, BasicBlock>();
            private List<BasicBlockBranch> branches_ = new List<BasicBlockBranch>();
            private BasicBlock start_ = new BasicBlock(true);
            private BasicBlock end_ = new BasicBlock(false);

            public ControlFlowGraph Build(List<BasicBlock> blocks) {
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

                for (int i = 0; i < blocks.Count; i++) {
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
                            case BoundNodeType.LabelStatement:
                                if (isLastStatement)
                                    Connect(current, next);
                                break;
                            default:
                                throw new Exception($"unexpected statement: {statement.type}");
                        }
                    }
                }

            ScanAgain:
                foreach (var block in blocks) {
                    if (!block.incoming.Any()) {
                        RemoveBlock(blocks, block);
                        goto ScanAgain;
                    }
                }

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

                var op = BoundUnaryOperator.Bind(SyntaxType.BANG_TOKEN, TypeSymbol.Bool);
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

        public void WriteTo(TextWriter writer) {
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

        public static ControlFlowGraph Create(BoundBlockStatement body) {
            var basicBlockBuilder = new BasicBlockBuilder();
            var blocks = basicBlockBuilder.Build(body);

            var graphBuilder = new GraphBuilder();
            return graphBuilder.Build(blocks);
        }

        public static bool AllPathsReturn(BoundBlockStatement body) {
            var graph = Create(body);

            foreach (var branch in graph.end.incoming) {
                var lastStatement = branch.from.statements.LastOrDefault();
                if (lastStatement == null || lastStatement.type != BoundNodeType.ReturnStatement)
                    return false;
            }

            return true;
        }
    }
}
