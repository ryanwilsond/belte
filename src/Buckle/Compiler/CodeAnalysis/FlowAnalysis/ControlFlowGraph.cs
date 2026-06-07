using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Creates a graphical control flow graph from a <see cref="BasicBlock" />.
/// </summary>
internal sealed class ControlFlowGraph {
    private readonly Dictionary<Symbol, int> _slotMap;
    private readonly ArrayBuilder<Symbol> _symbolsBySlot;
    private readonly MultiDictionary<Symbol, Symbol> _closureCaptures;

    private MethodSymbol _method;

    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" />.
    /// </summary>
    /// <param name="start">Start of the graph.</param>
    /// <param name="end">End of the graph.</param>
    /// <param name="blocks">All blocks within the graph.</param>
    /// <param name="branch">All branches within the graph.</param>
    internal ControlFlowGraph(
        BasicBlock start,
        BasicBlock end,
        List<BasicBlock> blocks,
        List<ControlFlowBranch> branch,
        Dictionary<Symbol, int> slotMap,
        ArrayBuilder<Symbol> symbolsBySlot) {
        this.start = start;
        this.end = end;
        this.blocks = blocks;
        branches = branch;
        _symbolsBySlot = symbolsBySlot;
        _slotMap = slotMap;
        _closureCaptures = [];
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
    /// All BasicBlocks in the graph.
    /// </summary>
    internal List<BasicBlock> blocks { get; }

    /// <summary>
    /// All BasicBlockBranches in the graph.
    /// </summary>
    internal List<ControlFlowBranch> branches { get; }

    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" /> from a <see cref="BoundBlockStatement" />.
    /// </summary>
    /// <param name="body"><see cref="BoundBlockStatement" /> to create from.</param>
    /// <returns><see cref="ControlFlowGraph" />.</returns>
    internal static ControlFlowGraph Create(BoundBlockStatement body) {
        var (slotMap, symbolsBySlot) = SlotCounter.Count(body);
        var basicBlockBuilder = new ControlFlowGraphBuilder.BasicBlockBuilder(symbolsBySlot.Count);
        var blocks = basicBlockBuilder.Build(body);
        var graphBuilder = new ControlFlowGraphBuilder(slotMap, symbolsBySlot);
        var controlFlowGraph = graphBuilder.Build(blocks, basicBlockBuilder.regions);
        return controlFlowGraph;
    }

    /// <summary>
    /// Checks if all code paths in a body return.
    /// </summary>
    /// <returns>If all code paths return.</returns>
    internal bool AllPathsReturn() {
        foreach (var branch in end.incoming) {
            var lastStatement = branch.from.statements.LastOrDefault();

            if (lastStatement is null)
                return false;

            var lastStatementIsThrow = lastStatement.kind == BoundKind.UnreachableStatement ||
                (lastStatement is BoundExpressionStatement es && es.expression is BoundThrowExpression);

            if (lastStatement.kind != BoundKind.ReturnStatement && !lastStatementIsThrow)
                return false;
        }

        return true;
    }

    internal HashSet<Symbol> CheckDefiniteAssignment(MethodSymbol method, BelteDiagnosticQueue diagnostics) {
        bool changed;
        _method = method;
        BelteDiagnosticQueue currentDiagnostics = null;

        do {
            changed = false;
            currentDiagnostics?.Free();
            currentDiagnostics = BelteDiagnosticQueue.GetInstance();

            foreach (var block in blocks) {
                var newIn = ComputeIn(block);
                var newOut = Transfer(block, newIn, currentDiagnostics);

                if (!newIn.Equals(block.incomingAssignment) ||
                    !newOut.Equals(block.outgoingAssignment)) {
                    block.incomingAssignment = newIn;
                    block.outgoingAssignment = newOut;
                    changed = true;
                }
            }
        } while (changed);

        diagnostics.PushRangeAndFree(currentDiagnostics);

        var set = new HashSet<Symbol>();
        var definiteAssignment = end.outgoingAssignment;

        for (var i = 0; i < definiteAssignment.capacity; i++) {
            if (definiteAssignment[i])
                set.Add(_symbolsBySlot[i]);
        }

        _symbolsBySlot.Free();

        return set;
    }

    private BitVector ComputeIn(BasicBlock block) {
        if (block == start)
            return BitVector.Empty;

        var first = true;
        var result = BitVector.Null;

        foreach (var branch in block.incoming) {
            var incoming = branch.from.outgoingAssignment.Clone();
            incoming.UnionWith(branch.flowState.assigned);

            if (first) {
                result = incoming;
                first = false;
            } else {
                result.IntersectWith(incoming);
            }
        }

        return result;
    }

    private BitVector Transfer(BasicBlock block, BitVector input, BelteDiagnosticQueue diagnostics) {
        var result = input.Clone();

        foreach (var statement in block.statements)
            ApplyStatement(statement, ref result, diagnostics);

        return result;
    }

    private void ApplyStatement(BoundStatement statement, ref BitVector result, BelteDiagnosticQueue diagnostics) {
        switch (statement.kind) {
            case BoundKind.ConditionalGotoStatement:
                ApplyExpression(((BoundConditionalGotoStatement)statement).condition, ref result, diagnostics);
                break;
            case BoundKind.SwitchDispatch:
                ApplyExpression(((BoundSwitchDispatch)statement).expression, ref result, diagnostics);
                break;
            case BoundKind.ReturnStatement:
                ApplyExpression(((BoundReturnStatement)statement).expression, ref result, diagnostics);
                break;
            case BoundKind.ExpressionStatement:
                ApplyExpression(((BoundExpressionStatement)statement).expression, ref result, diagnostics);
                break;
            case BoundKind.LocalDeclarationStatement:
                var declaration = ((BoundLocalDeclarationStatement)statement).declaration;

                if (declaration.initializer is not null) {
                    ApplyExpression(declaration.initializer, ref result, diagnostics);
                    var symbol = declaration.dataContainer;
                    result[_slotMap[symbol]] = true;
                }

                break;
            case BoundKind.TryStatement:
            case BoundKind.InlineILStatement:
            case BoundKind.LabelStatement:
            case BoundKind.LocalFunctionStatement:
            case BoundKind.UnreachableStatement:
            case BoundKind.NopStatement:
            case BoundKind.GotoStatement:
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(statement.kind);
        }
    }

    private void ApplyExpression(BoundExpression expression, ref BitVector result, BelteDiagnosticQueue diagnostics) {
        if (expression is null)
            return;

        switch (expression.kind) {
            case BoundKind.AssignmentOperator:
                ApplyAssignment((BoundAssignmentOperator)expression, ref result, diagnostics);
                break;
            case BoundKind.CastExpression:
                ApplyExpression(((BoundCastExpression)expression).operand, ref result, diagnostics);
                break;
            case BoundKind.DataContainerExpression: {
                    var symbol = ((BoundDataContainerExpression)expression).dataContainer;
                    // TODO This is a hack to avoid reporting for pattern locals which aren't analyzed correctly
                    var shouldReport = symbol.declarationKind == DataContainerDeclarationKind.Variable &&
                        !symbol.isGlobal &&
                        (_method is SynthesizedMethodSymbolBase m ? m.baseMethod : _method.originalDefinition)
                            .Equals(symbol.containingSymbol);

                    if (shouldReport && !result[_slotMap[symbol]])
                        diagnostics.Push(Error.UseOfUnassignedLocal(expression.syntax.location, symbol));

                    break;
                }
            case BoundKind.FieldAccessExpression: {
                    var fieldAccess = (BoundFieldAccessExpression)expression;
                    ApplyExpression(fieldAccess.receiver, ref result, diagnostics);
                    var field = fieldAccess.field;

                    if (fieldAccess.receiver?.type is SynthesizedClosureEnvironment &&
                        fieldAccess.receiver.expressionSymbol is not null) {
                        _closureCaptures.Add(fieldAccess.receiver.expressionSymbol, field);
                    }

                    var shouldReport = _method.IsConstructor() && ((_method.isStatic && field.isStatic) ||
                        (!_method.isStatic && Binder.IsThisInstanceAccess(fieldAccess))) &&
                        !(field.containingType.IsStructType() && field.type.HasDefaultValue()) &&
                        !_method.HasThisConstructorInitializer();

                    if (shouldReport && field.definiteAssignmentError is not null && !result[_slotMap[field]])
                        diagnostics.Push(Error.UseOfUnassignedField(fieldAccess.syntax.location, field));

                    break;
                }
            case BoundKind.UnaryOperator:
                ApplyExpression(((BoundUnaryOperator)expression).operand, ref result, diagnostics);
                break;
            case BoundKind.BinaryOperator:
                var binary = (BoundBinaryOperator)expression;
                ApplyExpression(binary.left, ref result, diagnostics);
                ApplyExpression(binary.right, ref result, diagnostics);
                break;
            case BoundKind.AsOperator:
                var asOp = (BoundAsOperator)expression;
                ApplyExpression(asOp.left, ref result, diagnostics);
                ApplyExpression(asOp.right, ref result, diagnostics);
                break;
            case BoundKind.IsOperator:
                var isOp = (BoundIsOperator)expression;
                ApplyExpression(isOp.left, ref result, diagnostics);
                ApplyExpression(isOp.right, ref result, diagnostics);
                break;
            case BoundKind.AddressOfOperator:
                ApplyExpression(((BoundAddressOfOperator)expression).operand, ref result, diagnostics);
                break;
            case BoundKind.PointerIndirectionOperator:
                ApplyExpression(((BoundPointerIndirectionOperator)expression).operand, ref result, diagnostics);
                break;
            case BoundKind.ConditionalOperator:
                var condOp = (BoundConditionalOperator)expression;
                ApplyExpression(condOp.condition, ref result, diagnostics);
                var trueVector = result.Clone();
                var falseVector = result.Clone();
                ApplyExpression(condOp.trueExpression, ref trueVector, diagnostics);
                ApplyExpression(condOp.falseExpression, ref falseVector, diagnostics);
                trueVector.IntersectWith(falseVector);
                result.UnionWith(trueVector);
                break;
            case BoundKind.NullAssertOperator:
                ApplyExpression(((BoundNullAssertOperator)expression).operand, ref result, diagnostics);
                break;
            case BoundKind.CallExpression:
                var call = (BoundCallExpression)expression;
                ApplyExpression(call.receiver, ref result, diagnostics);
                ApplyExpressionList(call.arguments, ref result, diagnostics);

                var expressionSymbol = call.receiver?.expressionSymbol;

                if (expressionSymbol is not null && _closureCaptures.ContainsKey(expressionSymbol)) {
                    foreach (var capture in _closureCaptures[expressionSymbol]) {
                        if (!result[_slotMap[capture]]) {
                            diagnostics.Push(Error.UseOfUnassignedLocal(
                                expression.syntax.location,
                                capture is LambdaCapturedVariable l ? l.captured : capture
                            ));
                        }
                    }
                }

                break;
            case BoundKind.ObjectCreationExpression:
                ApplyExpressionList(((BoundObjectCreationExpression)expression).arguments, ref result, diagnostics);
                break;
            case BoundKind.ArrayCreationExpression:
                ApplyExpressionList(((BoundArrayCreationExpression)expression).sizes, ref result, diagnostics);
                break;
            case BoundKind.ArrayAccessExpression:
                var arrayAccess = (BoundArrayAccessExpression)expression;
                ApplyExpression(arrayAccess.receiver, ref result, diagnostics);
                ApplyExpression(arrayAccess.index, ref result, diagnostics);
                break;
            case BoundKind.IndexerAccessExpression:
                var indexerAccess = (BoundIndexerAccessExpression)expression;
                ApplyExpression(indexerAccess.receiver, ref result, diagnostics);
                ApplyExpression(indexerAccess.index, ref result, diagnostics);
                break;
            case BoundKind.ThrowExpression:
                ApplyExpression(((BoundThrowExpression)expression).expression, ref result, diagnostics);
                break;
            case BoundKind.FunctionPointerCallExpression:
                ApplyExpressionList(((BoundFunctionPointerCallExpression)expression).arguments, ref result, diagnostics);
                break;
            case BoundKind.ConvertedStackAllocExpression:
                ApplyExpression(((BoundConvertedStackAllocExpression)expression).count, ref result, diagnostics);
                break;
            case BoundKind.CompileTimeExpression:
                ApplyExpression(((BoundCompileTimeExpression)expression).expression, ref result, diagnostics);
                break;
            case BoundKind.ThisExpression:
            case BoundKind.DefaultExpression:
            case BoundKind.BaseExpression:
            case BoundKind.ParameterExpression:
            case BoundKind.FunctionPointerLoad:
            case BoundKind.FunctionLoad:
            case BoundKind.TypeOfExpression:
            case BoundKind.SizeOfOperator:
            case BoundKind.TypeExpression:
            case BoundKind.MethodGroup:
            case BoundKind.LiteralExpression:
            case BoundKind.ErrorExpression:
                break;
            case BoundKind.StackSlotExpression:
            case BoundKind.FieldSlotExpression:
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private void ApplyAssignment(
        BoundAssignmentOperator assignment,
        ref BitVector result,
        BelteDiagnosticQueue diagnostics) {
        ApplyExpression(assignment.right, ref result, diagnostics);
        var left = assignment.left;

        switch (left.kind) {
            case BoundKind.AssignmentOperator:
                ApplyAssignment((BoundAssignmentOperator)left, ref result, diagnostics);
                break;
            case BoundKind.DataContainerExpression: {
                    var symbol = ((BoundDataContainerExpression)left).dataContainer;
                    result[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.ParameterExpression: {
                    var symbol = ((BoundParameterExpression)left).parameter;
                    result[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.FieldSlotExpression: {
                    var fieldSlot = (BoundFieldSlotExpression)left;
                    ApplyExpression(fieldSlot.receiver, ref result, diagnostics);
                    var symbol = fieldSlot.field;
                    result[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.FieldAccessExpression: {
                    var fieldAccess = (BoundFieldAccessExpression)left;
                    ApplyExpression(fieldAccess.receiver, ref result, diagnostics);
                    var symbol = fieldAccess.field;
                    result[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.ArrayAccessExpression:
            case BoundKind.ThisExpression:
            case BoundKind.BaseExpression:
            case BoundKind.CallExpression:
            case BoundKind.ConditionalOperator:
            case BoundKind.FunctionPointerCallExpression:
            case BoundKind.ThrowExpression:
            case BoundKind.PointerIndirectionOperator:
            default:
                break;
            case BoundKind.StackSlotExpression:
                throw ExceptionUtilities.UnexpectedValue(left.kind);
        }
    }

    private void ApplyExpressionList(
        ImmutableArray<BoundExpression> expressions,
        ref BitVector result,
        BelteDiagnosticQueue diagnostics) {
        foreach (var expression in expressions)
            ApplyExpression(expression, ref result, diagnostics);
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

        for (var i = 0; i < blocks.Count; i++) {
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
}
