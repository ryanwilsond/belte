using System.Collections.Generic;
using Buckle.CodeAnalysis.Lowering;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class StackFrame {
    internal StackFrame(EvaluatorSlotManager layout) {
        this.layout = layout;
        values = new List<EvaluatorValue>(layout.LocalsInOrder().Length);
    }

    internal EvaluatorSlotManager layout { get; }

    internal List<EvaluatorValue> values { get; }
}
