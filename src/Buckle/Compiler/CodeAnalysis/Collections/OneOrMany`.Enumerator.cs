namespace Buckle.CodeAnalysis;

internal readonly partial struct OneOrMany<T> {
    public struct Enumerator {
        private readonly OneOrMany<T> _collection;
        private int _index;

        internal Enumerator(OneOrMany<T> collection) {
            _collection = collection;
            _index = -1;
        }

        internal bool MoveNext() {
            _index++;
            return _index < _collection.Count;
        }

        public T Current => _collection[_index];
    }
}
