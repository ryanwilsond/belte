using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class WeakList<T> where T : class {
    internal struct Enumerator {
        private readonly WeakList<T> _weakList;
        private readonly int _count;
        private int _nextIndex;
        private int _alive;
        private int _firstDead;
        private T? _current;

        internal Enumerator(WeakList<T> weakList) {
            _weakList = weakList;
            _nextIndex = 0;
            _count = weakList._size;
            _alive = weakList._size;
            _firstDead = -1;
            _current = null;
        }

        internal T Current => _current;

        internal bool MoveNext() {
            while (_nextIndex < _count) {
                var currentIndex = _nextIndex;
                _nextIndex += 1;

                if (_weakList._items[currentIndex].TryGetTarget(out var item)) {
                    _current = item;
                    return true;
                } else {
                    if (_firstDead < 0)
                        _firstDead = currentIndex;

                    _alive--;
                }
            }

            if (_alive == 0) {
                _weakList._items = Array.Empty<WeakReference<T>>();
                _weakList._size = 0;
            } else if (_alive < _weakList._items.Length / 4) {
                _weakList.Shrink(_firstDead, _alive);
            }

            return false;
        }
    }
}
