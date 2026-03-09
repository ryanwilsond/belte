using System;
using System.Collections.Generic;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

internal class Heap {
    private readonly Stack<int> _freedIndices;
    private int _bumpPointer;
    private int _gcThreshold;
    private HeapObject[] _data;

    internal Heap(int initialCapacity = 64) {
        _data = new HeapObject[initialCapacity];
        _freedIndices = new Stack<int>();
        _gcThreshold = Math.Max(initialCapacity / 2, 32);
    }

    internal HeapObject this[int index] {
        get {
            if (index >= _data.Length)
                throw new IndexOutOfRangeException();

            return _data[index];
        }
        set {
            if (index >= _data.Length)
                throw new IndexOutOfRangeException();

            _data[index] = value;
        }
    }

    internal int capacity => _data.Length;

    internal int usedCount => _bumpPointer - _freedIndices.Count;

    internal void CleanHeap(Stack<StackFrame> stack, EvaluatorContext context) {
        foreach (var frame in stack)
            MarkMany(frame.values);

        MarkMany(context.GetTrackedGlobalObjects().Values);

        Sweep();

        void Sweep() {
            var data = _data;

            for (var i = 0; i < _bumpPointer; i++) {
                var obj = data[i];

                if (obj is null)
                    continue;

                if (!obj.markedForCollection)
                    Free(i);
                else
                    obj.markedForCollection = false;
            }
        }

        void MarkMany(IEnumerable<EvaluatorValue> values) {
            foreach (var value in values) {
                if (value.kind == ValueKind.HeapPtr)
                    Mark(value.ptr);
            }
        }

        void Mark(int ptr) {
            var markStack = new Stack<int>();
            markStack.Push(ptr);

            while (markStack.Count > 0) {
                var p = markStack.Pop();
                var obj = this[p];

                if (obj is null || obj.markedForCollection)
                    continue;

                obj.markedForCollection = true;

                foreach (var field in obj.fields) {
                    if (field.kind == ValueKind.HeapPtr)
                        markStack.Push(field.ptr);
                }
            }
        }
    }

    internal int Allocate(HeapObject item, Stack<StackFrame> stack, EvaluatorContext context) {
        if (_bumpPointer - _freedIndices.Count > _gcThreshold) {
            CleanHeap(stack, context);
            _gcThreshold = Math.Max(usedCount * 2, 64);
        }

        int index;

        if (_freedIndices.Count > 0) {
            index = _freedIndices.Pop();
        } else {
            index = _bumpPointer++;
            EnsureCapacity(index + 1);
        }

        _data[index] = item;
        return index;
    }

    internal void Free(int index) {
#if DEBUG
        if (index < 0 || index >= _bumpPointer)
            throw new BelteInternalException($"Attempted to free slot outside of allocated range ({index})");

        if (_data[index] is null)
            throw new BelteInternalException($"Attempted to free already free slot ({index})");
#else
        if (index < 0 || index >= _bumpPointer || _data[index] is null)
            return;
#endif

        _data[index] = null;
        _freedIndices.Push(index);
    }

    internal void FreeAll() {
        Array.Clear(_data, 0, _data.Length);
        _freedIndices.Clear();
        _bumpPointer = 0;
    }

    private void EnsureCapacity(int required) {
        if (required <= _data.Length)
            return;

        var newCapacity = Math.Max(required, _data.Length * 2);
        _gcThreshold = Math.Max(newCapacity / 2, 64);
        Array.Resize(ref _data, newCapacity);
    }
}
