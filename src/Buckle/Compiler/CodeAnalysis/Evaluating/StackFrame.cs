using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

internal class StackFrame {
    internal readonly List<int> heapAllocations;

    internal readonly Dictionary<Symbol, EvaluatorObject> locals;

    internal StackFrame() {
        locals = [];
        heapAllocations = [];
    }

    internal StackFrame(Dictionary<Symbol, EvaluatorObject> locals) {
        this.locals = locals;
        heapAllocations = [];
    }

    internal bool TryGetLocal(Symbol key, out EvaluatorObject value) {
        return locals.TryGetValue(key, out value);
    }

    internal bool ContainsLocal(Symbol key) {
        return locals.ContainsKey(key);
    }

    internal void AssignLocal(Symbol key, EvaluatorObject value) {
        locals[key] = value;
    }

    internal void MarkAllocation(int index) {
        heapAllocations.Add(index);
    }
}
