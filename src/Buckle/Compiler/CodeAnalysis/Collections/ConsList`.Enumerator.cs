using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class ConsList<T> : IEnumerable<T> {
    internal struct Enumerator : IEnumerator<T> {
        private T _current;
        private ConsList<T> _tail;

        internal Enumerator(ConsList<T> list) {
            _current = default;
            _tail = list;
        }

        public T Current => _current;

        public bool MoveNext() {
            var currentTail = _tail;
            var newTail = currentTail._tail;

            if (newTail is not null) {
                _current = currentTail._head;
                _tail = newTail;
                return true;
            }

            _current = default;
            return false;
        }

        public void Dispose() { }

        object? IEnumerator.Current => Current;

        public void Reset() {
            throw new NotSupportedException();
        }
    }
}
