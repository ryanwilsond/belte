using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract partial class LocalSlotManager {
    private Dictionary<Symbol, VariableDefinition> _localSymbolMap;
    private KeyedStack<LocalSignature, VariableDefinition> _freeSlotsStack;
    private protected ArrayBuilder<VariableDefinition> _lazyAllLocals;

    private protected Dictionary<Symbol, VariableDefinition> _localMap {
        get {
            var map = _localSymbolMap;

            if (map is null) {
                map = new Dictionary<Symbol, VariableDefinition>(ReferenceEqualityComparer.Instance);
                _localSymbolMap = map;
            }

            return map;
        }
    }

    private protected KeyedStack<LocalSignature, VariableDefinition> _freeSlots {
        get {
            var slots = _freeSlotsStack;

            if (slots is null) {
                slots = new KeyedStack<LocalSignature, VariableDefinition>();
                _freeSlotsStack = slots;
            }

            return slots;
        }
    }

    internal VariableDefinition GetLocal(Symbol symbol) {
        return _localMap[symbol];
    }

    internal void FreeLocal(Symbol symbol) {
        var slot = GetLocal(symbol);
        _localMap.Remove(symbol);
        FreeSlot(slot);
    }

    internal void FreeSlot(VariableDefinition slot) {
        _freeSlots.Push(new LocalSignature(slot.type, slot.constraints), slot);
    }

    internal ImmutableArray<VariableDefinition> LocalsInOrder() {
        if (_lazyAllLocals is null)
            return [];
        else
            return _lazyAllLocals.ToImmutable();
    }
}
