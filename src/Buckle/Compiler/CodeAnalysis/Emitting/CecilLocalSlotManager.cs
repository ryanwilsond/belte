using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilLocalSlotManager : LocalSlotManager {
    internal VariableDefinition DeclareLocal(
        Mono.Cecil.Cil.VariableDefinition variableDefinition,
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints,
        bool isSlotReusable) {
        if (!isSlotReusable || !_freeSlots.TryPop(new LocalSignature(type, constraints), out var local))
            local = DeclareLocalImpl(variableDefinition, type, symbol, name, kind, constraints);

        _localMap.Add(symbol, local);
        return local;
    }

    internal VariableDefinition AllocateSlot(
        Mono.Cecil.Cil.VariableDefinition variableDefinition,
        TypeSymbol type,
        LocalSlotConstraints constraints) {
        if (!_freeSlots.TryPop(new LocalSignature(type, constraints), out var local)) {
            local = DeclareLocalImpl(
                variableDefinition: variableDefinition,
                type: type,
                symbol: null,
                name: null,
                kind: SynthesizedLocalKind.EmitterTemp,
                constraints: constraints
            );
        }

        return local;
    }

    internal CecilVariableDefinition GetCecilLocal(DataContainerSymbol symbol) {
        return (CecilVariableDefinition)GetLocal(symbol);
    }

    private VariableDefinition DeclareLocalImpl(
        Mono.Cecil.Cil.VariableDefinition variableDefinition,
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints) {
        _lazyAllLocals ??= new ArrayBuilder<VariableDefinition>(1);

        var local = new CecilVariableDefinition(
            variableDefinition,
            symbol,
            name,
            type,
            _lazyAllLocals.Count,
            synthesizedKind: kind,
            constraints: constraints
        );

        _lazyAllLocals.Add(local);
        return local;
    }
}
