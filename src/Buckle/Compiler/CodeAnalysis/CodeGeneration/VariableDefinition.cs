
namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract class VariableDefinition {
    internal VariableDefinition(bool isRef) {
        this.isRef = isRef;
    }

    internal bool isRef { get; }
}
