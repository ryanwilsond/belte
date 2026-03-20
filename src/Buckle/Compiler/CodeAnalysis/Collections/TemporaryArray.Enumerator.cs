namespace Buckle.CodeAnalysis;

internal partial struct TemporaryArray<T> {
    // [NonCopyable]
    public struct Enumerator {
        private readonly TemporaryArray<T> _array;

        private T _current;
        private int _nextIndex;

        public Enumerator(in TemporaryArray<T> array) {
            // Enumerate a copy of the original
            _array = new TemporaryArray<T>(in array);
            _current = default!;
            _nextIndex = 0;
        }

        public T Current => _current;

        public bool MoveNext() {
            if (_nextIndex >= _array.Count) {
                return false;
            } else {
                _current = _array[_nextIndex];
                _nextIndex++;
                return true;
            }
        }
    }
}
