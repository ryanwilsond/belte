using System;
using Buckle.CodeAnalysis.Lowering;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class StackFrame {
    private EvaluatorValue[] _values;
    private int _resizeAddition = 2;

    internal StackFrame(EvaluatorSlotManager layout) {
        this.layout = layout;
        _values = new EvaluatorValue[layout.LocalsInOrder().Length];
    }

    internal EvaluatorSlotManager layout { get; }

    internal EvaluatorValue[] values => _values;

    internal void Resize() {
        Array.Resize(ref _values, values.Length + _resizeAddition);
        _resizeAddition *= 2;
    }
}
