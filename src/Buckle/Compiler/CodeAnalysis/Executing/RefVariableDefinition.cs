using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefVariableDefinition : VariableDefinition {
    internal RefVariableDefinition(LocalBuilder localBuilder, bool isRef) : base(isRef) {
        this.localBuilder = localBuilder;
    }

    internal LocalBuilder localBuilder { get; }
}
