using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefVariableDefinition : VariableDefinition {
    internal RefVariableDefinition(
        LocalBuilder localBuilder,
        DataContainerSymbol symbol,
        string name,
        TypeSymbol type,
        int slot,
        SynthesizedLocalKind synthesizedKind,
        LocalSlotConstraints constraints)
        : base(symbol, name, type, slot, synthesizedKind, constraints) {
        this.localBuilder = localBuilder;
    }

    internal LocalBuilder localBuilder { get; }
}
