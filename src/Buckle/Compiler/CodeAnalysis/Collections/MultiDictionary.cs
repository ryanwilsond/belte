using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal sealed partial class MultiDictionary<K, V> : IEnumerable<KeyValuePair<K, MultiDictionary<K, V>.ValueSet>>
    where K : notnull {
    private readonly Dictionary<K, ValueSet> _dictionary;
    private readonly IEqualityComparer<V> _valueComparer;

    internal MultiDictionary() {
        _dictionary = [];
    }

    internal MultiDictionary(IEqualityComparer<K> comparer) {
        _dictionary = new Dictionary<K, ValueSet>(comparer);
    }

    internal void EnsureCapacity(int capacity) {
        _dictionary.EnsureCapacity(capacity);
    }

    internal MultiDictionary(int capacity, IEqualityComparer<K> comparer, IEqualityComparer<V>? valueComparer = null) {
        _dictionary = new Dictionary<K, ValueSet>(capacity, comparer);
        _valueComparer = valueComparer;
    }

    public int Count => _dictionary.Count;

    public bool IsEmpty => _dictionary.Count == 0;

    public Dictionary<K, ValueSet>.KeyCollection Keys => _dictionary.Keys;

    public Dictionary<K, ValueSet>.ValueCollection Values => _dictionary.Values;

    private readonly ValueSet _emptySet = new(null, null);

    public ValueSet this[K k] => _dictionary.TryGetValue(k, out var set) ? set : _emptySet;

    public bool Add(K k, V v) {
        ValueSet updated;

        if (_dictionary.TryGetValue(k, out var set)) {
            updated = set.Add(v);
            if (updated.Equals(set)) {
                return false;
            }
        } else {
            updated = new ValueSet(v, _valueComparer);
        }

        _dictionary[k] = updated;
        return true;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public Dictionary<K, ValueSet>.Enumerator GetEnumerator() {
        return _dictionary.GetEnumerator();
    }

    IEnumerator<KeyValuePair<K, ValueSet>> IEnumerable<KeyValuePair<K, ValueSet>>.GetEnumerator() {
        return GetEnumerator();
    }

    public bool ContainsKey(K k) {
        return _dictionary.ContainsKey(k);
    }

    internal void Clear() {
        _dictionary.Clear();
    }

    public void Remove(K key) {
        _dictionary.Remove(key);
    }
}
