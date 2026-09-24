using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly ArrayBuilder<FieldSymbol> _fieldsRequiringAssignment;
    private readonly ArrayBuilder<PropertySymbol> _propertiesRequiringAssignment;

    private BelteDiagnosticQueue _diagnostics;
    private BitVector _assignments;

    private DefiniteAssignment(
        Dictionary<Symbol, int> slotMap,
        MethodSymbol containingMethod,
        MultiDictionary<Symbol, Symbol> closureCaptures,
        ArrayBuilder<FieldSymbol> fieldsRequiringAssignment,
        ArrayBuilder<PropertySymbol> propertiesRequiringAssignment) {
        _slotMap = slotMap;
        _method = containingMethod;
        _closureCaptures = closureCaptures;
        _fieldsRequiringAssignment = fieldsRequiringAssignment;
        _propertiesRequiringAssignment = propertiesRequiringAssignment;
    }

    internal static HashSet<Symbol> CheckDefiniteAssignment(
        ControlFlowGraph graph,
        ArrayBuilder<Symbol> symbolsBySlot,
        Dictionary<Symbol, int> slotMap,
        MethodSymbol method,
        MultiDictionary<Symbol, Symbol> closureCaptures,
        ArrayBuilder<FieldSymbol> fieldsRequiringAssignment,
        ArrayBuilder<PropertySymbol> propertiesRequiringAssignment,
        BelteDiagnosticQueue diagnostics) {
        bool changed;
        BelteDiagnosticQueue currentDiagnostics = null;
        var walker = new DefiniteAssignment(
            slotMap,
            method,
            closureCaptures,
            fieldsRequiringAssignment,
            propertiesRequiringAssignment
        );

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

                if (symbol is FieldSymbol or PropertySymbol)
                    set.Add(symbolsBySlot[i]);
            }
        }

        var definiteAssignmentLocals = end.outgoingAssignment;

        for (var i = 0; i < definiteAssignmentLocals.capacity; i++) {
            if (definiteAssignmentLocals[i]) {
                var symbol = symbolsBySlot[i];

                if (symbol.kind is SymbolKind.Local or SymbolKind.Parameter)
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

        var shouldReport = !symbol.isGlobal &&
            (_method is SynthesizedMethodSymbolBase m ? m.baseMethod : _method.originalDefinition)
                .Equals(symbol.containingSymbol)
                    // TODO This is a hack to avoid reporting for pattern locals which aren't analyzed correctly
                    && symbol.declarationKind == DataContainerDeclarationKind.Variable
            ;

        if (shouldReport && !_assignments[_slotMap[symbol]])
            _diagnostics.Push(Error.UseOfUnassignedLocal(node.syntax.location, symbol));

        return node;
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        var symbol = node.parameter;

        var shouldReport = symbol.refKind == RefKind.Out && _method.originalDefinition.Equals(symbol.containingSymbol);

        if (shouldReport && !_assignments[_slotMap[symbol]])
            _diagnostics.Push(Error.UseOfUnassignedOutParameter(node.syntax.location, symbol.name));

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

        foreach (var field in node.method.initFields) {
            if (_method.containingType.originalDefinition.Equals(node.method.containingType.originalDefinition))
                _assignments[_slotMap[field]] = true;
            else if (_slotMap.TryGetValue(field, out var slot))
                _assignments[slot] = true;
        }

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

        // TODO Also warn on static constructors?
        if (_method.methodKind == MethodKind.Constructor && Binder.IsThisInstanceAccess(node.receiver)) {
            Debug.Assert(_fieldsRequiringAssignment is not null && _propertiesRequiringAssignment is not null);

            // Technically a method with init fields could leak stuff
            if (!node.method.IsConstructor() && node.method.initFields.IsDefaultOrEmpty) {
                foreach (var field in _fieldsRequiringAssignment) {
                    if (!field.isStatic) {
                        if (!_assignments[_slotMap[field]]) {
                            _diagnostics.Push(Warning.PotentialUninitializedObjectLeak(node.syntax.location));
                            break;
                        }
                    }
                }

                foreach (var property in _propertiesRequiringAssignment) {
                    if (!property.isSealed) {
                        if (!_assignments[_slotMap[property]]) {
                            _diagnostics.Push(Warning.PotentialUninitializedObjectLeak(node.syntax.location));
                            break;
                        }
                    }
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

                    if (symbol is SynthesizedBackingFieldSymbol) {
                        Debug.Assert(symbol.associatedSymbol is PropertySymbol);
                        _assignments[_slotMap[symbol.associatedSymbol]] = true;
                    }

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
