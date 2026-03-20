using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal sealed partial class MultiDictionary<K, V> where K : notnull {
    internal readonly partial struct ValueSet : IEnumerable<V> {
        private readonly object _value;
        private readonly IEqualityComparer<V> _equalityComparer;

        public ValueSet(object? value, IEqualityComparer<V>? equalityComparer = null) {
            _value = value;
            _equalityComparer = equalityComparer ?? ImmutableHashSet<V>.Empty.KeyComparer;
        }

        public int Count {
            get {
                if (_value is null)
                    return 0;

                if (_value is not ImmutableHashSet<V> set)
                    return 1;

                return set.Count;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator<V> IEnumerable<V>.GetEnumerator() {
            return GetEnumerator();
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        public ValueSet Add(V v) {
            var set = _value as ImmutableHashSet<V>;

            if (set is null) {
                if (_equalityComparer.Equals((V)_value!, v))
                    return this;

                set = ImmutableHashSet.Create(_equalityComparer, (V)_value!);
            }

            return new ValueSet(set.Add(v), _equalityComparer);
        }

        public bool Contains(V v) {
            if (_value is not ImmutableHashSet<V> set)
                return _equalityComparer.Equals((V)_value!, v);

            return set.Contains(v);
        }

        public bool Contains(V v, IEqualityComparer<V> comparer) {
            foreach (var other in this) {
                if (comparer.Equals(other, v))
                    return true;
            }

            return false;
        }

        public V Single() {
            return (V)_value;
        }

        public bool Equals(ValueSet other) {
            return _value == other._value;
        }
    }
}
