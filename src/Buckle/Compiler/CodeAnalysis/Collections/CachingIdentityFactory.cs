using System;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal class CachingIdentityFactory<TKey, TValue> : CachingBase<CachingIdentityFactory<TKey, TValue>.Entry>
    where TKey : class {
    private readonly Func<TKey, TValue> _valueFactory;
    private readonly ObjectPool<CachingIdentityFactory<TKey, TValue>> _pool;

    internal struct Entry {
        internal TKey key;
        internal TValue value;
    }

    internal CachingIdentityFactory(int size, Func<TKey, TValue> valueFactory) : base(size) {
        _valueFactory = valueFactory;
    }

    internal CachingIdentityFactory(int size, Func<TKey, TValue> valueFactory, ObjectPool<CachingIdentityFactory<TKey, TValue>> pool)
        : this(size, valueFactory) {
        _pool = pool;
    }

    internal void Add(TKey key, TValue value) {
        var hash = RuntimeHelpers.GetHashCode(key);
        var idx = hash & _mask;

        _lazyEntries[idx].key = key;
        _lazyEntries[idx].value = value;
    }

    internal bool TryGetValue(TKey key, out TValue value) {
        var hash = RuntimeHelpers.GetHashCode(key);
        var idx = hash & _mask;

        var entries = _lazyEntries;

        if ((object)entries[idx].key == (object)key) {
            value = entries[idx].value;
            return true;
        }

        value = default;
        return false;
    }

    internal TValue GetOrMakeValue(TKey key) {
        var hash = RuntimeHelpers.GetHashCode(key);
        var idx = hash & _mask;

        var entries = _lazyEntries;

        if ((object)entries[idx].key == (object)key)
            return entries[idx].value;

        var value = _valueFactory(key);
        entries[idx].key = key;
        entries[idx].value = value;

        return value;
    }

    internal static ObjectPool<CachingIdentityFactory<TKey, TValue>> CreatePool(int size, Func<TKey, TValue> valueFactory) {
        var pool = new ObjectPool<CachingIdentityFactory<TKey, TValue>>(
            pool => new CachingIdentityFactory<TKey, TValue>(size, valueFactory, pool),
            Environment.ProcessorCount * 2);

        return pool;
    }

    internal void Free() {
        var pool = _pool;
        pool?.Free(this);
    }
}
