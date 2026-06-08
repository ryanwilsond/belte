using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.FlowAnalysis;

internal sealed class SlotCounter : BoundTreeWalker {
    private readonly Dictionary<Symbol, int> _slotMap = [];
    private readonly ArrayBuilder<Symbol> _symbolsBySlot = [];

    internal static (Dictionary<Symbol, int>, ArrayBuilder<Symbol>) Count(MethodSymbol method, BoundNode node) {
        var slotCounter = new SlotCounter();

        foreach (var member in method.containingType.GetMembers()) {
            if (member is FieldSymbol f && f.isStatic == method.isStatic)
                slotCounter.GetOrAddSlot(f);
        }

        slotCounter.Visit(node);
        return (slotCounter._slotMap, slotCounter._symbolsBySlot);
    }

    private void GetOrAddSlot(Symbol symbol) {
        if (_slotMap.ContainsKey(symbol))
            return;

        var slot = _symbolsBySlot.Count;
        _symbolsBySlot.Add(symbol);
        _slotMap.Add(symbol, slot);
    }

    internal override BoundNode VisitDataContainerDeclaration(BoundDataContainerDeclaration node) {
        GetOrAddSlot(node.dataContainer);
        return base.VisitDataContainerDeclaration(node);
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        GetOrAddSlot(node.dataContainer);
        return base.VisitDataContainerExpression(node);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        GetOrAddSlot(node.field);
        return base.VisitFieldAccessExpression(node);
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        GetOrAddSlot(node.parameter);
        return base.VisitParameterExpression(node);
    }

    internal override BoundNode VisitStackSlotExpression(BoundStackSlotExpression node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitFieldSlotExpression(BoundFieldSlotExpression node) {
        throw ExceptionUtilities.Unreachable();
    }
}
