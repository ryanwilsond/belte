using System;
using System.Collections.Generic;
using System.Linq;

namespace Buckle.CodeAnalysis.Evaluating;

internal class Heap {
    private readonly Evaluator _evaluator;
    private readonly Stack<int> _freeIndices;
    private EvaluatorObject[] _data;

    internal Heap(Evaluator evaluator, int initialCapacity = 16) {
        _evaluator = evaluator;
        _data = new EvaluatorObject[initialCapacity];
        _freeIndices = new Stack<int>();
    }

    internal EvaluatorObject this[int index] {
        get {
            EnsureCapacity(index + 1);
            return _data[index];
        }
        set {
            EnsureCapacity(index + 1);
            _data[index] = value;
        }
    }

    internal int capacity => _data.Length;

    internal int usedCount => _data.Count(d => d is null);

    internal int Allocate(EvaluatorObject item) {
        if (capacity - _freeIndices.Count >= 2048)
            _evaluator.CleanHeap();

        int index;

        if (_freeIndices.Count > 0) {
            index = _freeIndices.Pop();
        } else {
            index = FindNextFreeIndex();
            EnsureCapacity(index + 1);
        }

        _data[index] = item;
        return index;
    }

    internal void Free(int index) {
        if (index >= 0 && index < _data.Length) {
            _data[index] = null;
            _freeIndices.Push(index);
        }
    }

    private void EnsureCapacity(int required) {
        if (required <= _data.Length)
            return;

        var newCapacity = Math.Max(required, _data.Length * 2);
        Array.Resize(ref _data, newCapacity);
    }

    private int FindNextFreeIndex() {
        for (var i = 0; i < _data.Length; i++) {
            if (_data[i] is null)
                return i;
        }

        return _data.Length;
    }
}
