using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Buckle.CodeAnalysis;

internal sealed class ConcurrentCache<TKey, TValue> : CachingBase<ConcurrentCache<TKey, TValue>.Entry>
    where TKey : notnull {
    private readonly IEqualityComparer<TKey> _keyComparer;

    internal class Entry {
        internal readonly int hash;
        internal readonly TKey key;
        internal readonly TValue value;

        internal Entry(int hash, TKey key, TValue value) {
            this.hash = hash;
            this.key = key;
            this.value = value;
        }
    }

    internal ConcurrentCache(int size, IEqualityComparer<TKey> keyComparer) : base(size, createBackingArray: false) {
        _keyComparer = keyComparer;
    }

    internal ConcurrentCache(int size) : this(size, EqualityComparer<TKey>.Default) { }

    internal bool TryAdd(TKey key, TValue value) {
        var hash = _keyComparer.GetHashCode(key);
        var idx = hash & _mask;

        var entry = _lazyEntries[idx];
        if (entry is not null && entry.hash == hash && _keyComparer.Equals(entry.key, key)) {
            return false;
        }

        _lazyEntries[idx] = new Entry(hash, key, value);
        return true;
    }

    internal bool TryGetValue(TKey key, [MaybeNullWhen(returnValue: false)] out TValue value) {
        var hash = _keyComparer.GetHashCode(key);
        var idx = hash & _mask;
        var entry = _lazyEntries[idx];

        if (entry is not null && entry.hash == hash && _keyComparer.Equals(entry.key, key)) {
            value = entry.value;
            return true;
        }

        value = default!;
        return false;
    }
}
