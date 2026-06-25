using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.FlowAnalysis;

internal sealed class DefiniteAssignment : BoundTreeWalkerWithStackGuard {
    private readonly Dictionary<Symbol, int> _slotMap;
    private readonly MultiDictionary<Symbol, Symbol> _closureCaptures;
    private readonly MethodSymbol _method;

    private BelteDiagnosticQueue _diagnostics;
    private BitVector _assignments;

    private DefiniteAssignment(
        Dictionary<Symbol, int> slotMap,
        MethodSymbol containingMethod,
        MultiDictionary<Symbol, Symbol> closureCaptures) {
        _slotMap = slotMap;
        _method = containingMethod;
        _closureCaptures = closureCaptures;
    }

    internal static HashSet<Symbol> CheckDefiniteAssignment(
        ControlFlowGraph graph,
        ArrayBuilder<Symbol> symbolsBySlot,
        Dictionary<Symbol, int> slotMap,
        MethodSymbol method,
        MultiDictionary<Symbol, Symbol> closureCaptures,
        BelteDiagnosticQueue diagnostics) {
        bool changed;
        BelteDiagnosticQueue currentDiagnostics = null;
        var walker = new DefiniteAssignment(slotMap, method, closureCaptures);

        var blocks = graph.blocks;
        var end = graph.end;
        var start = graph.start;

        do {
            changed = false;
            currentDiagnostics?.Free();
            currentDiagnostics = BelteDiagnosticQueue.GetInstance();

            foreach (var block in blocks) {
                var newIn = ComputeIn(block, start);
                var newOut = Transfer(walker, block, newIn, currentDiagnostics);

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
        var definiteAssignmentFields = blocks[^2].outgoingAssignment;

        for (var i = 0; i < definiteAssignmentFields.capacity; i++) {
            if (definiteAssignmentFields[i]) {
                var symbol = symbolsBySlot[i];

                if (symbol is FieldSymbol)
                    set.Add(symbolsBySlot[i]);
            }
        }

        var definiteAssignmentLocals = end.outgoingAssignment;

        for (var i = 0; i < definiteAssignmentLocals.capacity; i++) {
            if (definiteAssignmentLocals[i]) {
                var symbol = symbolsBySlot[i];

                if (symbol is DataContainerSymbol)
                    set.Add(symbolsBySlot[i]);
            }
        }

        return set;
    }

    private static BitVector ComputeIn(BasicBlock block, BasicBlock start) {
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

    private static BitVector Transfer(
        DefiniteAssignment walker,
        BasicBlock block,
        BitVector input,
        BelteDiagnosticQueue diagnostics) {
        var result = input.Clone();

        walker._diagnostics = diagnostics;
        walker._assignments = result;

        foreach (var statement in block.statements)
            walker.Visit(statement);

        return walker._assignments;
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var declaration = node.declaration;

        if (declaration.initializer is not null) {
            Visit(declaration.initializer);
            var symbol = declaration.dataContainer;
            _assignments[_slotMap[symbol]] = true;
            return node;
        }

        return base.VisitLocalDeclarationStatement(node);
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        var symbol = node.dataContainer;

        // TODO This is a hack to avoid reporting for pattern locals which aren't analyzed correctly
        var shouldReport = symbol.declarationKind == DataContainerDeclarationKind.Variable &&
            !symbol.isGlobal &&
            (_method is SynthesizedMethodSymbolBase m ? m.baseMethod : _method.originalDefinition)
                .Equals(symbol.containingSymbol);

        if (shouldReport && !_assignments[_slotMap[symbol]])
            _diagnostics.Push(Error.UseOfUnassignedLocal(node.syntax.location, symbol));

        return node;
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        Visit(node.receiver);
        var field = node.field;

        if (node.receiver?.type is SynthesizedClosureEnvironment &&
            node.receiver.expressionSymbol is not null) {
            _closureCaptures.Add(node.receiver.expressionSymbol, field);
        }

        var shouldReport = _method.IsConstructor() && ((_method.isStatic && field.isStatic) ||
            (!_method.isStatic && Binder.IsThisInstanceAccess(node))) &&
            !(field.containingType.IsStructType() && field.type.HasDefaultValue()) &&
            !_method.HasThisConstructorInitializer();

        if (shouldReport && field.definiteAssignmentError is not null && !_assignments[_slotMap[field]])
            _diagnostics.Push(Error.UseOfUnassignedField(node.syntax.location, field));

        return node;
    }

    internal override BoundNode VisitConditionalOperator(BoundConditionalOperator node) {
        Visit(node.condition);
        var result = _assignments;

        _assignments = result.Clone();
        Visit(node.trueExpression);
        var trueVector = _assignments;

        _assignments = result.Clone();
        Visit(node.falseExpression);
        var falseVector = _assignments;

        trueVector.IntersectWith(falseVector);
        result.UnionWith(trueVector);
        _assignments = result;

        return node;
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        Visit(node.receiver);
        VisitList(node.arguments);

        foreach (var field in node.method.initFields)
            _assignments[_slotMap[field]] = true;

        var expressionSymbol = node.receiver?.expressionSymbol;

        if (expressionSymbol is not null && _closureCaptures.ContainsKey(expressionSymbol)) {
            foreach (var capture in _closureCaptures[expressionSymbol]) {
                if (!_assignments[_slotMap[capture]]) {
                    _diagnostics.Push(Error.UseOfUnassignedLocal(
                        node.syntax.location,
                        capture is LambdaCapturedVariable l ? l.captured : capture
                    ));
                }
            }
        }

        return node;
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        ApplyAssignment(node);
        return node;
    }

    private void ApplyAssignment(BoundAssignmentOperator assignment) {
        Visit(assignment.right);
        var left = assignment.left;

        switch (left.kind) {
            case BoundKind.AssignmentOperator:
                ApplyAssignment((BoundAssignmentOperator)left);
                break;
            case BoundKind.DataContainerExpression: {
                    var symbol = ((BoundDataContainerExpression)left).dataContainer;
                    _assignments[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.ParameterExpression: {
                    var symbol = ((BoundParameterExpression)left).parameter;
                    _assignments[_slotMap[symbol]] = true;
                    break;
                }
            case BoundKind.FieldAccessExpression: {
                    var fieldAccess = (BoundFieldAccessExpression)left;
                    Visit(fieldAccess.receiver);
                    var symbol = fieldAccess.field;
                    _assignments[_slotMap[symbol]] = true;
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
            case BoundKind.FieldSlotExpression:
                throw ExceptionUtilities.UnexpectedValue(left.kind);
        }
    }
}
