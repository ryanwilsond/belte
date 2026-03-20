using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

// [NonCopyable]
[StructLayout(LayoutKind.Sequential)]
internal partial struct TemporaryArray<T> : IDisposable {
    private const int InlineCapacity = 4;

    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;

    private int _count;
    private ArrayBuilder<T> _builder;

    private TemporaryArray(in TemporaryArray<T> array) {
        this = array;
    }

    internal static TemporaryArray<T> GetInstance(int capacity) {
        if (capacity <= InlineCapacity)
            return Empty;

        return new TemporaryArray<T>() {
            _builder = ArrayBuilder<T>.GetInstance(capacity)
        };
    }

    internal static TemporaryArray<T> Empty => default;

    public readonly int Count => _builder?.Count ?? _count;

    internal T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get {
            if (_builder is not null)
                return _builder[index];

            if ((uint)index >= _count)
                ThrowIndexOutOfRangeException();

            return index switch {
                0 => _item0,
                1 => _item1,
                2 => _item2,
                _ => _item3,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            if (_builder is not null) {
                _builder[index] = value;
                return;
            }

            if ((uint)index >= _count)
                ThrowIndexOutOfRangeException();

            _ = index switch {
                0 => _item0 = value,
                1 => _item1 = value,
                2 => _item2 = value,
                _ => _item3 = value,
            };
        }
    }

    public void Dispose() {
        Interlocked.Exchange(ref _builder, null)?.Free();
    }

    internal void Add(T item) {
        if (_builder is not null) {
            _builder.Add(item);
        } else if (_count < InlineCapacity) {
            _count++;
            this[_count - 1] = item;
        } else {
            MoveInlineToBuilder();
            _builder.Add(item);
        }
    }

    internal void AddRange(ImmutableArray<T> items) {
        if (_builder is not null) {
            _builder.AddRange(items);
        } else if (_count + items.Length <= InlineCapacity) {
            foreach (var item in items) {
                _count++;
                this[_count - 1] = item;
            }
        } else {
            MoveInlineToBuilder();
            _builder.AddRange(items);
        }
    }

    internal void AddRange(in TemporaryArray<T> items) {
        if (_count + items.Count <= InlineCapacity) {
            foreach (var item in items) {
                _count++;
                this[_count - 1] = item;
            }
        } else {
            MoveInlineToBuilder();
            foreach (var item in items)
                _builder.Add(item);
        }
    }

    internal void Clear() {
        if (_builder is not null)
            _builder.Clear();
        else
            this = Empty;
    }

    internal T RemoveLast() {
        var count = Count;

        var last = this[count - 1];
        this[count - 1] = default!;

        if (_builder is not null)
            _builder.Count--;
        else
            _count--;

        return last;
    }

    internal readonly bool Contains(T value, IEqualityComparer<T> equalityComparer = null) {
        return IndexOf(value, equalityComparer) >= 0;
    }

    internal readonly int IndexOf(T value, IEqualityComparer<T> equalityComparer = null) {
        equalityComparer ??= EqualityComparer<T>.Default;

        if (_builder is not null)
            return _builder.IndexOf(value, equalityComparer);

        var index = 0;

        foreach (var v in this) {
            if (equalityComparer.Equals(v, value))
                return index;

            index++;
        }

        return -1;
    }

    public readonly Enumerator GetEnumerator() {
        return new Enumerator(in this);
    }

    internal OneOrMany<T> ToOneOrManyAndClear() {
        switch (Count) {
            case 0:
                return OneOrMany<T>.Empty;
            case 1:
                var result = OneOrMany.Create(this[0]);
                Clear();
                return result;
            default:
                return new(ToImmutableAndClear());
        }
    }

    internal ImmutableArray<T> ToImmutableAndClear() {
        if (_builder is not null) {
            return _builder.ToImmutableAndClear();
        } else {
            var result = _count switch {
                0 => ImmutableArray<T>.Empty,
                1 => [_item0],
                2 => [_item0, _item1],
                3 => [_item0, _item1, _item2],
                4 => [_item0, _item1, _item2, _item3],
                _ => throw ExceptionUtilities.Unreachable(),
            };

            this = Empty;
            return result;
        }
    }

    [MemberNotNull(nameof(_builder))]
    private void MoveInlineToBuilder() {
        var builder = ArrayBuilder<T>.GetInstance();

        for (var i = 0; i < _count; i++) {
            builder.Add(this[i]);

#if NET
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
#endif
            {
                this[i] = default!;
            }
        }

        _count = 0;
        _builder = builder;
    }

    internal void ReverseContents() {
        if (_builder is not null) {
            _builder.ReverseContents();
            return;
        }

        switch (_count) {
            case <= 1:
                return;
            case 2:
                (_item0, _item1) = (_item1, _item0);
                return;
            case 3:
                (_item0, _item2) = (_item2, _item0);
                return;
            case 4:
                (_item0, _item1, _item2, _item3) = (_item3, _item2, _item1, _item0);
                return;
            default:
                throw ExceptionUtilities.Unreachable();
        }
    }

    internal void Sort(Comparison<T> compare) {
        if (_builder is not null) {
            _builder.Sort(compare);
            return;
        }

        switch (_count) {
            case <= 1:
                return;
            case 2:
                if (compare(_item0, _item1) > 0) {
                    (_item0, _item1) = (_item1, _item0);
                }
                return;
            case 3:
                if (compare(_item0, _item1) > 0)
                    (_item0, _item1) = (_item1, _item0);

                if (compare(_item1, _item2) > 0) {
                    (_item1, _item2) = (_item2, _item1);

                    if (compare(_item0, _item1) > 0)
                        (_item0, _item1) = (_item1, _item0);
                }
                return;
            case 4:

                if (compare(_item0, _item1) > 0)
                    (_item0, _item1) = (_item1, _item0);

                if (compare(_item2, _item3) > 0)
                    (_item2, _item3) = (_item3, _item2);

                if (compare(_item0, _item2) > 0)
                    (_item0, _item2) = (_item2, _item0);

                if (compare(_item1, _item3) > 0)
                    (_item1, _item3) = (_item3, _item1);

                if (compare(_item1, _item2) > 0)
                    (_item1, _item2) = (_item2, _item1);

                return;
            default:
                throw ExceptionUtilities.Unreachable();
        }
    }

    private static void ThrowIndexOutOfRangeException() {
        throw new IndexOutOfRangeException();
    }
}
