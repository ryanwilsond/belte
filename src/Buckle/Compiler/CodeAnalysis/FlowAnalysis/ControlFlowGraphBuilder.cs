using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Builds a <see cref="ControlFlowGraph" /> from BasicBlocks and BasicBlockBranches.
/// </summary>
internal sealed partial class ControlFlowGraphBuilder {
    private readonly Dictionary<BoundStatement, BasicBlock> _blockFromStatement = [];
    private readonly Dictionary<LabelSymbol, BasicBlock> _blockFromLabel = [];
    private readonly List<ControlFlowBranch> _branches = [];
    private readonly BasicBlock _start = new BasicBlock(true);
    private readonly BasicBlock _end = new BasicBlock(false);
    private List<BasicBlock> _blocks = [];
    private readonly Dictionary<BasicBlock, List<TryRegion>> _regionsByBlock = [];

    internal ControlFlowGraph Build(List<BasicBlock> blocks, List<TryRegion> regions) {
        _blocks = blocks;

        if (blocks.Count == 0)
            Connect(_start, _end);
        else
            Connect(_start, blocks[0]);

        foreach (var block in blocks) {
            foreach (var statement in block.statements) {
                _blockFromStatement.Add(statement, block);

                if (statement is BoundLabelStatement labelStatement)
                    _blockFromLabel.TryAdd(labelStatement.label, block);
            }
        }

        foreach (var region in regions) {
            foreach (var block in blocks) {
                if (IsInRange(block, region))
                    _regionsByBlock.GetOrAdd(block, () => []).Add(region);
            }
        }

        for (var i = 0; i < blocks.Count; i++) {
            var current = blocks[i];
            var next = i == blocks.Count - 1 ? _end : blocks[i + 1];

            foreach (var statement in current.statements) {
                var isLastStatement = statement == current.statements.Last();

                switch (statement.kind) {
                    case BoundKind.GotoStatement:
                        var gs = (BoundGotoStatement)statement;
                        var toBlock = _blockFromLabel[gs.label];

                        Connect(current, toBlock);

                        break;
                    case BoundKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)statement;

                        if (!cgs.condition.IsLiteralNull()) {
                            var thenBlock = _blockFromLabel[cgs.label];
                            var elseBlock = next;
                            var negatedCondition = Negate(cgs.condition);
                            var thenCondition = cgs.jumpIfTrue ? cgs.condition : negatedCondition;
                            var elseCondition = cgs.jumpIfTrue ? negatedCondition : cgs.condition;

                            Connect(current, thenBlock, thenCondition);
                            Connect(current, elseBlock, elseCondition);
                        }

                        break;
                    case BoundKind.ReturnStatement:
                        Connect(current, _end);
                        break;
                    case BoundKind.ExpressionStatement when
                        ((BoundExpressionStatement)statement).expression is BoundThrowExpression:
                        AddThrowEdges(current);
                        break;
                    case BoundKind.NopStatement:
                    case BoundKind.ExpressionStatement:
                    case BoundKind.SwitchDispatch:
                    case BoundKind.InlineILStatement:
                    case BoundKind.LocalDeclarationStatement:
                    case BoundKind.LabelStatement:
                    case BoundKind.TryStatement:
                    case BoundKind.LocalFunctionStatement:
                        if (isLastStatement)
                            Connect(current, next);

                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(statement.kind);
                }
            }

            var last = current.statements.LastOrDefault();

            if (last is null || !IsTerminal(last)) {
                foreach (var region in GetActiveRegions(current)) {
                    if (current == region.tryStart)
                        ConnectDirect(current, region.tryEnd);

                    if (region.catchBlock is not null)
                        ConnectDirect(current, region.catchBlock);

                    // TODO Do finallys ever effect flow analysis?
                    // if (region.finallyBlock is not null)
                    //     ConnectDirect(current, region.finallyBlock);
                }
            }
        }

again:
        foreach (var block in blocks) {
            if (block.incoming.Count == 0) {
                RemoveBlock(blocks, block);
                goto again;
            }
        }

        blocks.Insert(0, _start);
        blocks.Add(_end);

        return new ControlFlowGraph(_start, _end, blocks, _branches);
    }

    private void AddThrowEdges(BasicBlock block) {
        var regions = GetActiveRegions(block);

        for (var i = regions.Count - 1; i >= 0; i--) {
            var region = regions[i];

            if (region.catchBlock is not null) {
                ConnectDirect(block, region.catchBlock);
                return;
            }

            // TODO Do finallys ever effect flow analysis?
            // if (region.finallyBlock is not null) {
            //     ConnectDirect(block, region.finallyBlock);
            //     return;
            // }
        }

        ConnectDirect(block, _end);
    }

    private bool IsInRange(BasicBlock block, TryRegion region) {
        var index = _blocks.IndexOf(block);
        var start = _blocks.IndexOf(region.tryStart);
        var end = _blocks.IndexOf(region.tryEnd);
        // If end is not in the block list it refers to the end of the graph which hasn't been added yet
        return end == -1 || (index >= start && index <= end);
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
        var syntax = condition.syntax;

        if (condition.constantValue is not null)
            return condition;

        var boolType = CorLibrary.GetSpecialType(SpecialType.Bool);
        var opKind = OverloadResolution.UnOpEasyOut.OpKind(UnaryOperatorKind.LogicalNegation, boolType);

        return new BoundUnaryOperator(syntax, condition, opKind, null, null, boolType);
    }

    private bool IsTerminal(BoundStatement statement) {
        return statement.kind is BoundKind.ReturnStatement
            || (statement is BoundExpressionStatement es && es.expression is BoundThrowExpression);
    }

    private void Connect(BasicBlock from, BasicBlock to, BoundExpression condition = null) {
        if (ConstantValue.IsNull(condition?.constantValue))
            return;

        if (condition is BoundLiteralExpression l) {
            var value = (bool)l.constantValue.value;

            if (value)
                condition = null;
            else
                return;
        }

        ConnectDirect(from, to, condition);
    }

    private List<TryRegion> GetActiveRegions(BasicBlock block) {
        if (_regionsByBlock.TryGetValue(block, out var value))
            return value;

        return [];
    }

    // TODO With our current strategy of ignoring finallys, we don't need this
    // But we probably will later
    private void RewriteAndConnect(BasicBlock from, BasicBlock to, BoundExpression condition) {
        var regions = GetActiveRegions(from);

        for (var i = regions.Count - 1; i >= 0; i--) {
            var region = regions[i];

            if (region.finallyBlock is null)
                continue;

            if (IsLeavingRegion(from, to, region)) {
                ConnectDirect(from, region.finallyBlock);
                RewriteAndConnect(region.finallyBlock, to, condition);
                return;
            }
        }

        ConnectDirect(from, to, condition);
    }

    private bool IsLeavingRegion(BasicBlock from, BasicBlock to, TryRegion region) {
        var fromRegions = GetActiveRegions(from);
        var toRegions = GetActiveRegions(to);
        return fromRegions.Contains(region) && !toRegions.Contains(region);
    }

    private void ConnectDirect(BasicBlock from, BasicBlock to, BoundExpression condition = null) {
        if (ConstantValue.IsNull(condition?.constantValue))
            return;

        if (condition is BoundLiteralExpression l) {
            var value = (bool)l.constantValue.value;

            if (value)
                condition = null;
            else
                return;
        }

        var branch = new ControlFlowBranch(from, to, condition);

        from.outgoing.Add(branch);
        to.incoming.Add(branch);
        _branches.Add(branch);
    }
}
