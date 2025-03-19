using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
[DebuggerTypeProxy(typeof(OneOrMany<>.DebuggerProxy))]
internal readonly partial struct OneOrMany<T> {
    internal static readonly OneOrMany<T> Empty = new OneOrMany<T>([]);

    private readonly T _one;
    private readonly ImmutableArray<T> _many;

    internal OneOrMany(T one) {
        _one = one;
        _many = default;
    }

    internal OneOrMany(ImmutableArray<T> many) {
        if (many.IsDefault)
            throw new ArgumentNullException(nameof(many));

        if (many is [var item]) {
            _one = item;
            _many = default;
        } else {
            _one = default;
            _many = many;
        }
    }

    [MemberNotNullWhen(true, nameof(_one))]
    private bool hasOneItem => _many.IsDefault;

    public bool IsDefault => _one == null && _many.IsDefault;

    internal T this[int index] {
        get {
            if (hasOneItem) {
                if (index != 0)
                    throw new IndexOutOfRangeException();

                return _one;
            } else {
                return _many[index];
            }
        }
    }

    public int Count => hasOneItem ? 1 : _many.Length;

    public bool IsEmpty => Count == 0;

    internal OneOrMany<T> Add(T item) {
        return hasOneItem
            ? OneOrMany.Create(_one, item)
            : IsEmpty
                ? OneOrMany.Create(item)
                : OneOrMany.Create(_many.Add(item));
    }

    internal void AddRangeTo(ArrayBuilder<T> builder) {
        if (hasOneItem)
            builder.Add(_one);
        else
            builder.AddRange(_many);
    }

    internal bool Contains(T item) {
        return hasOneItem ? EqualityComparer<T>.Default.Equals(item, _one) : _many.Contains(item);
    }

    internal OneOrMany<T> RemoveAll(T item) {
        if (hasOneItem)
            return EqualityComparer<T>.Default.Equals(item, _one) ? Empty : this;

        return OneOrMany.Create(
            _many.WhereAsArray(static (value, item) => !EqualityComparer<T>.Default.Equals(value, item), item)
        );
    }

    internal OneOrMany<TResult> Select<TResult>(Func<T, TResult> selector) {
        return hasOneItem
            ? OneOrMany.Create(selector(_one))
            : OneOrMany.Create(_many.SelectAsArray(selector));
    }

    internal OneOrMany<TResult> Select<TResult, TArg>(Func<T, TArg, TResult> selector, TArg arg) {
        return hasOneItem
            ? OneOrMany.Create(selector(_one, arg))
            : OneOrMany.Create(_many.SelectAsArray(selector, arg));
    }

    internal T First() => this[0];

    internal T? FirstOrDefault() {
        return hasOneItem ? _one : _many.FirstOrDefault();
    }

    internal T? FirstOrDefault(Func<T, bool> predicate) {
        if (hasOneItem)
            return predicate(_one) ? _one : default;

        return _many.FirstOrDefault(predicate);
    }

    internal T? FirstOrDefault<TArg>(Func<T, TArg, bool> predicate, TArg arg) {
        if (hasOneItem)
            return predicate(_one, arg) ? _one : default;

        return _many.FirstOrDefault(predicate, arg);
    }

    internal static OneOrMany<T> CastUp<TDerived>(OneOrMany<TDerived> from) where TDerived : class, T {
        return from.hasOneItem
            ? new OneOrMany<T>(from._one)
            : new OneOrMany<T>(ImmutableArray<T>.CastUp(from._many));
    }

    internal bool All(Func<T, bool> predicate) {
        return hasOneItem ? predicate(_one) : _many.All(predicate);
    }

    internal bool All<TArg>(Func<T, TArg, bool> predicate, TArg arg) {
        return hasOneItem ? predicate(_one, arg) : _many.All(predicate, arg);
    }

    internal bool Any() {
        return !IsEmpty;
    }

    internal bool Any(Func<T, bool> predicate) {
        return hasOneItem ? predicate(_one) : _many.Any(predicate);
    }

    internal bool Any<TArg>(Func<T, TArg, bool> predicate, TArg arg) {
        return hasOneItem ? predicate(_one, arg) : _many.Any(predicate, arg);
    }

    internal ImmutableArray<T> ToImmutable() {
        return hasOneItem ? ImmutableArray.Create(_one) : _many;
    }

    internal T[] ToArray() {
        return hasOneItem ? new[] { _one } : _many.ToArray();
    }

    internal bool SequenceEqual(OneOrMany<T> other, IEqualityComparer<T>? comparer = null) {
        comparer ??= EqualityComparer<T>.Default;

        if (Count != other.Count) {
            return false;
        }

        Debug.Assert(hasOneItem == other.hasOneItem);

        return hasOneItem ? comparer.Equals(_one, other._one!) :
               System.Linq.ImmutableArrayExtensions.SequenceEqual(_many, other._many, comparer);
    }

    internal bool SequenceEqual(ImmutableArray<T> other, IEqualityComparer<T>? comparer = null) {
        return SequenceEqual(OneOrMany.Create(other), comparer);
    }

    internal bool SequenceEqual(IEnumerable<T> other, IEqualityComparer<T>? comparer = null) {
        comparer ??= EqualityComparer<T>.Default;

        if (!hasOneItem) {
            return _many.SequenceEqual(other, comparer);
        }

        var first = true;
        foreach (var otherItem in other) {
            if (!first || !comparer.Equals(_one, otherItem)) {
                return false;
            }

            first = false;
        }

        return true;
    }

    public Enumerator GetEnumerator() {
        return new(this);
    }

    private string GetDebuggerDisplay() {
        return "Count = " + Count;
    }
}
