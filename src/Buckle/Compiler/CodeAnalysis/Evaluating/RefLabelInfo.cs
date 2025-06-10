using System.Reflection.Emit;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefLabelInfo : CodeGeneration.LabelInfo {
    internal RefLabelInfo(ILGenerator generator) {
        label = generator.DefineLabel();
    }

    internal Label label { get; }
}
