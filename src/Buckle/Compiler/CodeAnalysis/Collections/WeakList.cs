using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class WeakList<T> where T : class {
    private WeakReference<T>[] _items;
    private int _size;

    internal WeakList() {
        _items = [];
    }

    private void Resize() {
        var alive = _items.Length;
        var firstDead = -1;

        for (var i = 0; i < _items.Length; i++) {
            if (!_items[i].TryGetTarget(out _)) {
                if (firstDead == -1)
                    firstDead = i;

                alive--;
            }
        }

        if (alive < _items.Length / 4) {
            Shrink(firstDead, alive);
        } else if (alive >= 3 * _items.Length / 4) {
            var newItems = new WeakReference<T>[GetExpandedSize(_items.Length)];

            if (firstDead >= 0)
                Compact(firstDead, newItems);
            else
                Array.Copy(_items, 0, newItems, 0, _items.Length);

            _items = newItems;
        } else {
            Compact(firstDead, _items);
        }
    }

    private void Shrink(int firstDead, int alive) {
        var newSize = GetExpandedSize(alive);
        var newItems = (newSize == _items.Length) ? _items : new WeakReference<T>[newSize];
        Compact(firstDead, newItems);
        _items = newItems;
    }

    private const int MinimalNonEmptySize = 4;

    private static int GetExpandedSize(int baseSize) {
        return Math.Max((baseSize * 2) + 1, MinimalNonEmptySize);
    }

    private void Compact(int firstDead, WeakReference<T>[] result) {
        if (!ReferenceEquals(_items, result))
            Array.Copy(_items, 0, result, 0, firstDead);

        var oldSize = _size;
        var j = firstDead;

        for (var i = firstDead + 1; i < oldSize; i++) {
            var item = _items[i];

            if (item.TryGetTarget(out _))
                result[j++] = item;
        }

        _size = j;

        if (ReferenceEquals(_items, result)) {
            while (j < oldSize)
                _items[j++] = null;
        }
    }

    internal int weakCount => _size;

    internal WeakReference<T> GetWeakReference(int index) {
        if (index < 0 || index >= _size) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _items[index];
    }

    internal void Add(T item) {
        if (_size == _items.Length)
            Resize();

        _items[_size++] = new WeakReference<T>(item);
    }

    internal Enumerator GetEnumerator() {
        return new Enumerator(this);
    }
}
