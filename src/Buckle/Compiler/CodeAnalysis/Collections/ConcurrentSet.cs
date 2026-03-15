using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("Count = {Count}")]
internal sealed partial class ConcurrentSet<T> : ICollection<T> where T : notnull {
    private const int DefaultConcurrencyLevel = 2;
    private const int DefaultCapacity = 31;

    private readonly ConcurrentDictionary<T, byte> _dictionary;

    public ConcurrentSet() {
        _dictionary = new ConcurrentDictionary<T, byte>(DefaultConcurrencyLevel, DefaultCapacity);
    }

    public ConcurrentSet(IEqualityComparer<T> equalityComparer) {
        _dictionary = new ConcurrentDictionary<T, byte>(DefaultConcurrencyLevel, DefaultCapacity, equalityComparer);
    }

    public int Count => _dictionary.Count;

    public bool isEmpty => _dictionary.IsEmpty;

    public bool IsReadOnly => false;

    public bool Contains(T value) {
        return _dictionary.ContainsKey(value);
    }

    public bool Add(T value) {
        return _dictionary.TryAdd(value, 0);
    }

    public void AddRange(IEnumerable<T>? values) {
        if (values != null) {
            foreach (var v in values) {
                Add(v);
            }
        }
    }

    public bool Remove(T value) {
        return _dictionary.TryRemove(value, out _);
    }

    public void Clear() {
        _dictionary.Clear();
    }

    public KeyEnumerator GetEnumerator() {
        return new KeyEnumerator(_dictionary);
    }

    private IEnumerator<T> GetEnumeratorImpl() {
        foreach (var kvp in _dictionary)
            yield return kvp.Key;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() {
        return GetEnumeratorImpl();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumeratorImpl();
    }

    void ICollection<T>.Add(T item) {
        Add(item);
    }

    public void CopyTo(T[] array, int arrayIndex) {
        foreach (var element in this)
            array[arrayIndex++] = element;
    }
}
