using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class ImmutableArrayExtensions {
    internal static Dictionary<K, ImmutableArray<T>> ToDictionary<K, T>(
        this ImmutableArray<T> items, Func<T, K> keySelector, IEqualityComparer<K>? comparer = null)
        where K : notnull {
        if (items.Length == 1) {
            var dictionary1 = new Dictionary<K, ImmutableArray<T>>(1, comparer);
            T value = items[0];
            dictionary1.Add(keySelector(value), ImmutableArray.Create(value));
            return dictionary1;
        }

        if (items.Length == 0)
            return new Dictionary<K, ImmutableArray<T>>(comparer);

        var accumulator = new Dictionary<K, ArrayBuilder<T>>(items.Length, comparer);

        for (int i = 0; i < items.Length; i++) {
            var item = items[i];
            var key = keySelector(item);

            if (!accumulator.TryGetValue(key, out var bucket)) {
                bucket = ArrayBuilder<T>.GetInstance();
                accumulator.Add(key, bucket);
            }

            bucket.Add(item);
        }

        var dictionary = new Dictionary<K, ImmutableArray<T>>(accumulator.Count, comparer);

        foreach (var pair in accumulator)
            dictionary.Add(pair.Key, pair.Value.ToImmutableAndFree());

        return dictionary;
    }
}
