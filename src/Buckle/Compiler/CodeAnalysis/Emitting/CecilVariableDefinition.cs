using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilVariableDefinition : VariableDefinition {
    internal CecilVariableDefinition(Mono.Cecil.Cil.VariableDefinition variableDefinition,
        DataContainerSymbol symbol,
        string name,
        TypeSymbol type,
        int slot,
        SynthesizedLocalKind synthesizedKind,
        LocalSlotConstraints constraints)
        : base(symbol, name, type, slot, synthesizedKind, constraints) {
        this.variableDefinition = variableDefinition;
    }

    internal Mono.Cecil.Cil.VariableDefinition variableDefinition { get; }
}
