using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal sealed partial class ConcurrentSet<T> where T : notnull {
    public readonly struct KeyEnumerator {
        private readonly IEnumerator<KeyValuePair<T, byte>> _kvpEnumerator;

        internal KeyEnumerator(IEnumerable<KeyValuePair<T, byte>> data) {
            _kvpEnumerator = data.GetEnumerator();
        }

        public T Current => _kvpEnumerator.Current.Key;

        public bool MoveNext() {
            return _kvpEnumerator.MoveNext();
        }

        public void Reset() {
            _kvpEnumerator.Reset();
        }
    }
}
