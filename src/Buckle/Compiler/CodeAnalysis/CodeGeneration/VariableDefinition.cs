using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract class VariableDefinition {
    internal VariableDefinition(
        DataContainerSymbol symbol,
        string name,
        TypeSymbol type,
        int slot,
        SynthesizedLocalKind synthesizedKind,
        LocalSlotConstraints constraints) {
        this.symbol = symbol;
        this.name = name;
        this.type = type;
        this.slot = slot;
        this.synthesizedKind = synthesizedKind;
        this.constraints = constraints;
    }

    internal DataContainerSymbol symbol { get; }

    internal string name { get; }

    internal TypeSymbol type { get; }

    internal int slot { get; }

    internal SynthesizedLocalKind synthesizedKind { get; }

    internal LocalSlotConstraints constraints { get; }

    internal bool isRef => (constraints & LocalSlotConstraints.ByRef) != 0;
}
