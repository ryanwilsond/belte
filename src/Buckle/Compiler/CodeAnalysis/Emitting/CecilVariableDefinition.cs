using Buckle.CodeAnalysis.CodeGeneration;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilVariableDefinition : VariableDefinition {
    internal CecilVariableDefinition(Mono.Cecil.Cil.VariableDefinition variableDefinition, bool isRef) : base(isRef) {
        this.variableDefinition = variableDefinition;
    }

    internal Mono.Cecil.Cil.VariableDefinition variableDefinition { get; }
}
