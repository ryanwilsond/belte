using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static class DictionaryExtensions {
    internal static TValue GetOrAdd<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue> getValue)
        where TKey : notnull {
        if (dictionary.TryGetValue(key, out var existingValue)) {
            return existingValue;
        } else {
            var value = getValue();
            dictionary.Add(key, value);
            return value;
        }
    }

    internal static TValue GetOrAdd<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
        where TKey : notnull {
        if (dictionary.TryGetValue(key, out var existingValue)) {
            return existingValue;
        } else {
            dictionary.Add(key, value);
            return value;
        }
    }
}
