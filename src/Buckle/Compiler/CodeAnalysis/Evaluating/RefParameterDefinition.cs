using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefParameterDefinition : ParameterDefinition {
    internal RefParameterDefinition(int slot) {
        this.slot = slot;
    }

    internal int slot { get; }
}
