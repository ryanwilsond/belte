using Buckle.CodeAnalysis.CodeGeneration;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilLabelInfo : LabelInfo {
    internal CecilLabelInfo(int targetInstructionIndex) {
        this.targetInstructionIndex = targetInstructionIndex;
    }

    internal int targetInstructionIndex { get; }
}
