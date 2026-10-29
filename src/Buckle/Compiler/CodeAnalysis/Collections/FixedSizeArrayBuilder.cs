using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

[NonCopyable]
internal struct FixedSizeArrayBuilder<T>(int capacity) {
    private T[] _values = new T[capacity];
    private int _index;

    public void Add(T value)
        => _values[_index++] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfTrue([DoesNotReturnIf(parameterValue: true)] bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? filePath = null) {
#if MICROSOFT_CODEANALYSIS_CONTRACTS_NO_CONTRACT
        if (condition)
        {
            var fileName = filePath is null ? null : Path.GetFileName(filePath);
            throw new InvalidOperationException($"Unexpected true - file {fileName} line {lineNumber}");
        }
#else
        // Contract.ThrowIfTrue(condition, lineNumber, filePath);
#endif
    }

    #region AddRange overloads.  These allow us to add these collections directly, without allocating an enumerator.

    public void AddRange(ImmutableArray<T> values) {
        ThrowIfTrue(_index + values.Length > _values.Length);
        Array.Copy(ImmutableCollectionsMarshal.AsArray(values)!, 0, _values, _index, values.Length);
        _index += values.Length;
    }

    public void AddRange(List<T> values) {
        ThrowIfTrue(_index + values.Count > _values.Length);
        foreach (var v in values)
            Add(v);
    }

    public void AddRange(HashSet<T> values) {
        ThrowIfTrue(_index + values.Count > _values.Length);
        foreach (var v in values)
            Add(v);
    }

    public void AddRange(ArrayBuilder<T> values) {
        ThrowIfTrue(_index + values.Count > _values.Length);
        foreach (var v in values)
            Add(v);
    }

    #endregion

    public void AddRange(IEnumerable<T> values) {
        foreach (var v in values)
            Add(v);
    }

    public readonly void Sort()
        => Sort(Comparer<T>.Default);

    public readonly void Sort(IComparer<T> comparer) {
        if (_index > 1)
            Array.Sort(_values, 0, _index, comparer);
    }

    public ImmutableArray<T> MoveToImmutable()
        => ImmutableCollectionsMarshal.AsImmutableArray(MoveToArray());

    public T[] MoveToArray() {
        ThrowIfTrue(_index != _values.Length);
        var result = _values;
        _values = [];
        _index = 0;
        return result;
    }
}
