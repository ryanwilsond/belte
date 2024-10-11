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

    internal static ImmutableArray<TValue> Flatten<TKey, TValue>(
        this Dictionary<TKey, ImmutableArray<TValue>> dictionary,
        IComparer<TValue> comparer = null)
        where TKey : notnull {
        if (dictionary.Count == 0)
            return [];

        var builder = ArrayBuilder<TValue>.GetInstance();

        foreach (var keyValuePair in dictionary)
            builder.AddRange(keyValuePair.Value);

        if (comparer is not null && builder.Count > 1)
            builder.Sort(comparer);

        return builder.ToImmutableAndFree();
    }

    internal static void AddToMultiValueDictionaryBuilder<K, T>(Dictionary<K, object> accumulator, K key, T item)
        where K : notnull
        where T : notnull {
        if (accumulator.TryGetValue(key, out var existingValueOrArray)) {
            if (existingValueOrArray is not ArrayBuilder<T> arrayBuilder) {
                arrayBuilder = ArrayBuilder<T>.GetInstance(capacity: 2);
                arrayBuilder.Add((T)existingValueOrArray);
                accumulator[key] = arrayBuilder;
            }

            arrayBuilder.Add(item);
        } else {
            accumulator.Add(key, item);
        }
    }

    internal static ImmutableArray<T> Concat<T>(this ImmutableArray<T> first, ImmutableArray<T> second) {
        return first.AddRange(second);
    }
}
