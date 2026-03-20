using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal class UnionCollection<T> : ICollection<T> {
    private readonly ImmutableArray<ICollection<T>> _collections;
    private int _count = -1;

    private UnionCollection(ImmutableArray<ICollection<T>> collections) {
        _collections = collections;
    }

    public int Count {
        get {
            if (_count == -1) {
                _count = _collections.Sum(c => c.Count);
            }

            return _count;
        }
    }

    public bool IsReadOnly => true;

    internal static ICollection<T> Create(ICollection<T> coll1, ICollection<T> coll2) {
        if (coll1.Count == 0)
            return coll2;

        if (coll2.Count == 0)
            return coll1;

        return new UnionCollection<T>([coll1, coll2]);
    }

    internal static ICollection<T> Create<TOrig>(ImmutableArray<TOrig> collections, Func<TOrig, ICollection<T>> selector) {
        return collections.Length switch {
            0 => (ICollection<T>)SpecializedCollections.EmptyCollection<T>(),
            1 => selector(collections[0]),
            _ => new UnionCollection<T>(ImmutableArray.CreateRange(collections, selector)),
        };
    }

    public void Add(T item) {
        throw new NotSupportedException();
    }

    public void Clear() {
        throw new NotSupportedException();
    }

    public bool Contains(T item) {
        foreach (var c in _collections) {
            if (c.Contains(item)) {
                return true;
            }
        }

        return false;
    }

    public void CopyTo(T[] array, int arrayIndex) {
        var index = arrayIndex;
        foreach (var collection in _collections) {
            collection.CopyTo(array, index);
            index += collection.Count;
        }
    }

    public bool Remove(T item) {
        throw new NotSupportedException();
    }

    public IEnumerator<T> GetEnumerator() {
        return _collections.SelectMany(c => c).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
