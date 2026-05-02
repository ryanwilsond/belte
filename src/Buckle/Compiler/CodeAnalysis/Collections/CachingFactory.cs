using System;

namespace Buckle.CodeAnalysis;

internal class CachingFactory<TKey, TValue> : CachingBase<CachingFactory<TKey, TValue>.Entry> where TKey : notnull {
    internal struct Entry {
        internal int hash;
        internal TValue value;
    }

    private readonly int _size;
    private readonly Func<TKey, TValue> _valueFactory;
    private readonly Func<TKey, int> _keyHash;
    private readonly Func<TKey, TValue, bool> _keyValueEquality;

    internal CachingFactory(
        int size,
        Func<TKey, TValue> valueFactory,
        Func<TKey, int> keyHash,
        Func<TKey, TValue, bool> keyValueEquality) :
        base(size) {
        _size = size;
        _valueFactory = valueFactory;
        _keyHash = keyHash;
        _keyValueEquality = keyValueEquality;
    }

    internal void Add(TKey key, TValue value) {
        var hash = GetKeyHash(key);
        var idx = hash & _mask;

        _lazyEntries[idx].hash = hash;
        _lazyEntries[idx].value = value;
    }

    internal bool TryGetValue(TKey key, out TValue value) {
        var hash = GetKeyHash(key);
        var idx = hash & _mask;

        var entries = _lazyEntries;
        if (entries[idx].hash == hash) {
            var candidate = entries[idx].value;
            if (_keyValueEquality(key, candidate)) {
                value = candidate;
                return true;
            }
        }

        value = default!;
        return false;
    }

    internal TValue GetOrMakeValue(TKey key) {
        var hash = GetKeyHash(key);
        var idx = hash & _mask;

        var entries = _lazyEntries;
        if (entries[idx].hash == hash) {
            var candidate = entries[idx].value;
            if (_keyValueEquality(key, candidate)) {
                return candidate;
            }
        }

        var value = _valueFactory(key);
        entries[idx].hash = hash;
        entries[idx].value = value;

        return value;
    }

    private int GetKeyHash(TKey key) {
        var result = _keyHash(key) | _size;
        return result;
    }
}
