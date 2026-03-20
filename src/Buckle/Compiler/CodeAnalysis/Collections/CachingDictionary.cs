using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal class CachingDictionary<TKey, TElement> where TKey : notnull {
    private static readonly ImmutableArray<TElement> EmptySentinel = [];

    private readonly Func<TKey, ImmutableArray<TElement>> _getElementsOfKey;
    private readonly Func<IEqualityComparer<TKey>, SegmentedHashSet<TKey>> _getKeys;
    private readonly IEqualityComparer<TKey> _comparer;

    private IDictionary<TKey, ImmutableArray<TElement>> _map;

    internal CachingDictionary(
        Func<TKey, ImmutableArray<TElement>> getElementsOfKey,
        Func<IEqualityComparer<TKey>, SegmentedHashSet<TKey>> getKeys,
        IEqualityComparer<TKey> comparer) {
        _getElementsOfKey = getElementsOfKey;
        _getKeys = getKeys;
        _comparer = comparer;
    }

    internal ImmutableArray<TElement> this[TKey key] => GetOrCreateValue(key);

    internal int Count => EnsureFullyPopulated().Count;

    internal IEnumerable<TKey> Keys => EnsureFullyPopulated().Keys;

    internal bool Contains(TKey key) {
        return this[key].Length != 0;
    }

    internal void AddValues(ArrayBuilder<TElement> array) {
        foreach (var kvp in EnsureFullyPopulated())
            array.AddRange(kvp.Value);
    }

    private ConcurrentDictionary<TKey, ImmutableArray<TElement>> CreateConcurrentDictionary() {
        return new ConcurrentDictionary<TKey, ImmutableArray<TElement>>(
            concurrencyLevel: 2,
            capacity: 0,
            comparer: _comparer
        );
    }

    private IDictionary<TKey, ImmutableArray<TElement>> CreateDictionaryForFullyPopulatedMap(int capacity) {
        return new Dictionary<TKey, ImmutableArray<TElement>>(capacity, _comparer);
    }

    private ImmutableArray<TElement> GetOrCreateValue(TKey key) {
        ConcurrentDictionary<TKey, ImmutableArray<TElement>>? concurrentMap;

        var localMap = _map;

        if (localMap == null) {
            concurrentMap = CreateConcurrentDictionary();
            localMap = Interlocked.CompareExchange(ref _map, concurrentMap, null);

            if (localMap is null)
                return AddToConcurrentMap(concurrentMap, key);
        }

        if (localMap.TryGetValue(key, out var elements))
            return elements;

        concurrentMap = localMap as ConcurrentDictionary<TKey, ImmutableArray<TElement>>;

        return concurrentMap is null ? EmptySentinel : AddToConcurrentMap(concurrentMap, key);
    }

    private ImmutableArray<TElement> AddToConcurrentMap(
        ConcurrentDictionary<TKey, ImmutableArray<TElement>> map,
        TKey key) {
        var elements = _getElementsOfKey(key);

        if (elements.IsDefaultOrEmpty)
            elements = EmptySentinel;

        return map.GetOrAdd(key, elements);
    }

    private static bool IsNotFullyPopulatedMap(IDictionary<TKey, ImmutableArray<TElement>> existingMap) {
        return existingMap is null || existingMap is ConcurrentDictionary<TKey, ImmutableArray<TElement>>;
    }

    private IDictionary<TKey, ImmutableArray<TElement>> CreateFullyPopulatedMap(
        ConcurrentDictionary<TKey, ImmutableArray<TElement>> existingMap) {
        var allKeys = _getKeys(_comparer);
        var fullyPopulatedMap = CreateDictionaryForFullyPopulatedMap(capacity: allKeys.Count);

        if (existingMap is null) {
            foreach (var key in allKeys)
                fullyPopulatedMap.Add(key, _getElementsOfKey(key));
        } else {
            foreach (var key in allKeys) {
                var elements = existingMap.GetOrAdd(key, _getElementsOfKey);
                fullyPopulatedMap.Add(key, elements);
            }
        }

        return fullyPopulatedMap;
    }

    private IDictionary<TKey, ImmutableArray<TElement>> EnsureFullyPopulated() {
        var currentMap = _map;

        while (IsNotFullyPopulatedMap(currentMap)) {
            var fullyPopulatedMap = CreateFullyPopulatedMap(
                (ConcurrentDictionary<TKey, ImmutableArray<TElement>>)currentMap
            );

            var replacedMap = Interlocked.CompareExchange(ref _map, fullyPopulatedMap, currentMap);

            if (replacedMap == currentMap)
                return fullyPopulatedMap;

            currentMap = replacedMap;
        }

        return currentMap;
    }
}
