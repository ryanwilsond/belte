using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefLocalSlotManager : LocalSlotManager {
    internal VariableDefinition DeclareLocal(
        LocalBuilder localBuilder,
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints,
        bool isSlotReusable) {
        if (!isSlotReusable || !_freeSlots.TryPop(new LocalSignature(type, constraints), out var local))
            local = DeclareLocalImpl(localBuilder, type, symbol, name, kind, constraints);

        _localMap.Add(symbol, local);
        return local;
    }

    internal VariableDefinition AllocateSlot(
        LocalBuilder localBuilder,
        TypeSymbol type,
        LocalSlotConstraints constraints) {
        if (!_freeSlots.TryPop(new LocalSignature(type, constraints), out var local)) {
            local = DeclareLocalImpl(
                localBuilder: localBuilder,
                type: type,
                symbol: null,
                name: null,
                kind: SynthesizedLocalKind.EmitterTemp,
                constraints: constraints
            );
        }

        return local;
    }

    internal RefVariableDefinition GetRefLocal(DataContainerSymbol symbol) {
        return (RefVariableDefinition)GetLocal(symbol);
    }

    private RefVariableDefinition DeclareLocalImpl(
        LocalBuilder localBuilder,
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints) {
        _lazyAllLocals ??= new ArrayBuilder<VariableDefinition>(1);

        var local = new RefVariableDefinition(
            localBuilder,
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
