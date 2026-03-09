using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorSlotManager : LocalSlotManager {
    internal EvaluatorSlotManager(Symbol symbol) {
        this.symbol = symbol;
    }

    internal Symbol symbol { get; }

    internal VariableDefinition DeclareLocal(
        TypeSymbol type,
        Symbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints,
        bool isSlotReusable) {
        if (!isSlotReusable || !_freeSlots.TryPop(new LocalSignature(type, constraints), out var local))
            local = DeclareLocalImpl(type, symbol, name, kind, constraints);

        _localMap.Add(symbol, local);
        return local;
    }

    internal VariableDefinition AllocateSlot(
        TypeSymbol type,
        LocalSlotConstraints constraints) {
        if (!_freeSlots.TryPop(new LocalSignature(type, constraints), out var local)) {
            local = DeclareLocalImpl(
                type: type,
                symbol: null,
                name: null,
                kind: SynthesizedLocalKind.EmitterTemp,
                constraints: constraints
            );
        }

        return local;
    }

    private VariableDefinition DeclareLocalImpl(
        TypeSymbol type,
        Symbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints) {
        _lazyAllLocals ??= new ArrayBuilder<VariableDefinition>(1);

        var local = new VariableDefinition(
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
