using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class ImmutableArrayExtensions {
    internal static Dictionary<K, ImmutableArray<T>> ToDictionary<K, T>(
        this ImmutableArray<T> items,
        Func<T, K> keySelector,
        IEqualityComparer<K>? comparer = null)
        where K : notnull {
        if (items.Length == 1) {
            var dictionary1 = new Dictionary<K, ImmutableArray<T>>(1, comparer);
            var value = items[0];
            dictionary1.Add(keySelector(value), ImmutableArray.Create(value));
            return dictionary1;
        }

        if (items.Length == 0)
            return new Dictionary<K, ImmutableArray<T>>(comparer);

        var accumulator = new Dictionary<K, ArrayBuilder<T>>(items.Length, comparer);

        for (var i = 0; i < items.Length; i++) {
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

    internal static ImmutableArray<TResult> SelectAsArray<TItem, TResult>(
        this ImmutableArray<TItem> items,
        Func<TItem, TResult> map) {
        return ImmutableArray.CreateRange(items, map);
    }

    internal static ImmutableArray<TResult> SelectAsArray<TItem, TArg, TResult>(
        this ImmutableArray<TItem> items,
        Func<TItem, TArg, TResult> map,
        TArg arg) {
        return ImmutableArray.CreateRange(items, map, arg);
    }

    internal static ImmutableArray<T> AsImmutableOrNull<T>(this T[]? items) {
        if (items == null)
            return default;

        return ImmutableArray.Create(items);
    }
}
