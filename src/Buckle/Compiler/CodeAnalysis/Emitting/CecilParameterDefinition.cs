using Buckle.CodeAnalysis.CodeGeneration;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilParameterDefinition : ParameterDefinition {
    internal CecilParameterDefinition(Mono.Cecil.ParameterDefinition parameterDefinition) {
        this.parameterDefinition = parameterDefinition;
    }

    internal Mono.Cecil.ParameterDefinition parameterDefinition { get; }
}
