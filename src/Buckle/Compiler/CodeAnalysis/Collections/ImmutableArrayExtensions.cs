using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class ImmutableArrayExtensions {
    internal static int BinarySearch<TElement, TValue>(
        this ImmutableArray<TElement> array,
        TValue value,
        Func<TElement, TValue, int> comparer)
            => BinarySearch(array.AsSpan(), value, comparer);

    internal static int BinarySearch<TElement, TValue>(
        this ReadOnlySpan<TElement> array,
        TValue value,
        Func<TElement, TValue, int> comparer) {
        var low = 0;
        var high = array.Length - 1;

        while (low <= high) {
            var middle = low + ((high - low) >> 1);
            var comparison = comparer(array[middle], value);

            if (comparison == 0)
                return middle;

            if (comparison > 0)
                high = middle - 1;
            else
                low = middle + 1;
        }

        return ~low;
    }

    internal static ImmutableArray<T> AsImmutable<T>(this IEnumerable<T> items) {
        return [.. items];
    }

    internal static ImmutableArray<T> AsImmutableOrNull<T>(this IEnumerable<T>? items) {
        if (items is null)
            return default;

        return [.. items];
    }

    internal static int Sum<T>(this ImmutableArray<T> items, Func<T, int> selector) {
        var sum = 0;

        foreach (var item in items)
            sum += selector(item);

        return sum;
    }

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

    internal static ImmutableArray<T> Distinct<T>(this ImmutableArray<T> array, IEqualityComparer<T>? comparer = null) {
        if (array.Length < 2)
            return array;

        var set = new HashSet<T>(comparer);
        var builder = ArrayBuilder<T>.GetInstance();

        foreach (var a in array) {
            if (set.Add(a)) {
                builder.Add(a);
            }
        }

        var result = (builder.Count == array.Length) ? array : builder.ToImmutable();
        builder.Free();
        return result;
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
        if (items is null)
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

    internal static bool All<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate) {
        // immutableArray.ThrowNullRefIfNotInitialized();
        // Requires.NotNull(predicate, nameof(predicate));

        foreach (var v in immutableArray!) {
            if (!predicate(v))
                return false;
        }

        return true;
    }

    internal static bool All<T, TArg>(this ImmutableArray<T> array, Func<T, TArg, bool> predicate, TArg arg) {
        var n = array.Length;

        for (var i = 0; i < n; i++) {
            var a = array[i];

            if (!predicate(a, arg))
                return false;
        }

        return true;
    }

    internal static ImmutableArray<T> NullToEmpty<T>(this ImmutableArray<T> array) {
        return array.IsDefault ? [] : array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImmutableArray<TBase> Cast<TDerived, TBase>(this ImmutableArray<TDerived> items)
        where TDerived : class, TBase {
        return ImmutableArray<TBase>.CastUp(items);
    }

    internal static void CreateNameToMembersMap<TKey, TNamespaceOrTypeSymbol, TNamedTypeSymbol, TNamespaceSymbol>
        (Dictionary<TKey, object> dictionary, Dictionary<TKey, ImmutableArray<TNamespaceOrTypeSymbol>> result)
        where TKey : notnull
        where TNamespaceOrTypeSymbol : class
        where TNamedTypeSymbol : class, TNamespaceOrTypeSymbol
        where TNamespaceSymbol : class, TNamespaceOrTypeSymbol {
        foreach (var entry in dictionary)
            result.Add(entry.Key, CreateMembers(entry.Value));

        return;

        static ImmutableArray<TNamespaceOrTypeSymbol> CreateMembers(object value) {
            if (value is ArrayBuilder<TNamespaceOrTypeSymbol> builder) {
                foreach (var item in builder) {
                    if (item is TNamespaceSymbol)
                        return builder.ToImmutableAndFree();
                }

                return ImmutableArray<TNamespaceOrTypeSymbol>.CastUp(
                    builder.ToDowncastedImmutableAndFree<TNamespaceOrTypeSymbol, TNamedTypeSymbol>()
                );
            } else {
                var symbol = (TNamespaceOrTypeSymbol)value;
                return symbol is TNamespaceSymbol
                    ? ImmutableArray.Create(symbol)
                    : ImmutableArray<TNamespaceOrTypeSymbol>.CastUp(ImmutableArray.Create((TNamedTypeSymbol)symbol));
            }
        }
    }

    internal static Dictionary<TKey, ImmutableArray<TNamedTypeSymbol>> GetTypesFromMemberMap
        <TKey, TNamespaceOrTypeSymbol, TNamedTypeSymbol>
        (Dictionary<TKey, ImmutableArray<TNamespaceOrTypeSymbol>> map, IEqualityComparer<TKey> comparer)
        where TKey : notnull
        where TNamespaceOrTypeSymbol : class
        where TNamedTypeSymbol : class, TNamespaceOrTypeSymbol {
        var capacity = map.Count > 3 ? map.Count : 0;

        var dictionary = new Dictionary<TKey, ImmutableArray<TNamedTypeSymbol>>(capacity, comparer);

        foreach (var entry in map) {
            var namedTypes = GetOrCreateNamedTypes(entry.Value);

            if (namedTypes.Length > 0)
                dictionary.Add(entry.Key, namedTypes);
        }

        return dictionary;

        static ImmutableArray<TNamedTypeSymbol> GetOrCreateNamedTypes(ImmutableArray<TNamespaceOrTypeSymbol> members) {
            var membersAsNamedTypes = members.As<TNamedTypeSymbol>();

            if (!membersAsNamedTypes.IsDefault)
                return membersAsNamedTypes;

            var count = members.Count(static s => s is TNamedTypeSymbol);

            if (count == 0)
                return [];

            var builder = ArrayBuilder<TNamedTypeSymbol>.GetInstance(count);

            foreach (var member in members) {
                if (member is TNamedTypeSymbol namedType)
                    builder.Add(namedType);
            }

            return builder.ToImmutableAndFree();
        }
    }

    internal static T FirstOrDefault<T>(this ImmutableArray<T> immutableArray) {
        // return immutableArray.array!.Length > 0 ? immutableArray.array[0] : default;
        // TODO Why can't we access `array` like in the above example?
        return immutableArray!.Length > 0 ? immutableArray[0] : default;
    }

    internal static T FirstOrDefault<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate) {
        // Requires.NotNull(predicate, nameof(predicate));

        foreach (var v in immutableArray!) {
            if (predicate(v))
                return v;
        }

        return default;
    }

    internal static int Count<T>(this ImmutableArray<T> items, Func<T, bool> predicate) {
        if (items.IsEmpty)
            return 0;

        var count = 0;

        for (var i = 0; i < items.Length; i++) {
            if (predicate(items[i]))
                ++count;
        }

        return count;
    }

    internal static ImmutableArray<T> WhereAsArray<T, TArg>(
        this ImmutableArray<T> array,
        Func<T, TArg, bool> predicate,
        TArg arg)
        => WhereAsArrayImpl(array, predicateWithoutArg: null, predicate, arg);

    internal static ImmutableArray<T> WhereAsArray<T>(this ImmutableArray<T> array, Func<T, bool> predicate)
        => WhereAsArrayImpl<T, object?>(array, predicate, predicateWithArg: null, arg: null);

    private static ImmutableArray<T> WhereAsArrayImpl<T, TArg>(
        ImmutableArray<T> array,
        Func<T, bool>? predicateWithoutArg,
        Func<T, TArg, bool>? predicateWithArg,
        TArg arg) {
        ArrayBuilder<T>? builder = null;
        var none = true;
        var all = true;

        var n = array.Length;
        for (var i = 0; i < n; i++) {
            var a = array[i];

            if ((predicateWithoutArg is not null) ? predicateWithoutArg(a) : predicateWithArg!(a, arg)) {
                none = false;

                if (all)
                    continue;

                builder ??= ArrayBuilder<T>.GetInstance();
                builder.Add(a);
            } else {
                if (none) {
                    all = false;
                    continue;
                }

                if (all) {
                    all = false;
                    builder = ArrayBuilder<T>.GetInstance();

                    for (var j = 0; j < i; j++)
                        builder.Add(array[j]);
                }
            }
        }

        if (builder is not null)
            return builder.ToImmutableAndFree();
        else if (all)
            return array;
        else
            return [];
    }
}
