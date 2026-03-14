using Buckle.CodeAnalysis.Lowering;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class StackFrame {
    internal StackFrame(EvaluatorSlotManager layout) {
        this.layout = layout;
        values = new EvaluatorValue[layout.LocalsInOrder().Length + layout.lateTempCount];
    }

    internal EvaluatorSlotManager layout { get; }

    internal EvaluatorValue[] values { get; }
}
