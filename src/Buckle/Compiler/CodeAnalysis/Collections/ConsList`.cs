using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class ConsList<T> : IEnumerable<T> {
    public static readonly ConsList<T> Empty = new();

    private readonly T _head;
    private readonly ConsList<T> _tail;

    private ConsList() {
        _head = default;
        _tail = null;
    }

    internal ConsList(T head, ConsList<T> tail) {
        _head = head;
        _tail = tail;
    }

    internal T head => _head!;

    internal ConsList<T> tail => _tail;

    internal bool ContainsReference(T element) {
        var list = this;

        for (; list != Empty; list = list.tail) {
            if (ReferenceEquals(list.head, element))
                return true;
        }

        return false;
    }

    internal bool Any() {
        return this != Empty;
    }

    internal ConsList<T> Push(T value) {
        return new ConsList<T>(value, this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() {
        return GetEnumerator();
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }
}
