using System;
using System.Collections.Concurrent;

namespace Buckle.CodeAnalysis;

internal static class ConcurrentDictionaryExtensions {
    internal static void Add<K, V>(this ConcurrentDictionary<K, V> dict, K key, V value) where K : notnull {
        if (!dict.TryAdd(key, value))
            throw new ArgumentException("adding a duplicate", nameof(key));
    }
}
