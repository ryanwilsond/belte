using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal static partial class IEnumerableExtensions {
    internal static bool IsEmpty<T>(this IEnumerable<T> source) {
        if (source is IReadOnlyCollection<T> readOnlyCollection)
            return readOnlyCollection.Count == 0;

        if (source is ICollection<T> genericCollection)
            return genericCollection.Count == 0;

        if (source is ICollection collection)
            return collection.Count == 0;

        if (source is string str)
            return str.Length == 0;

        foreach (var _ in source)
            return false;

        return true;
    }

    internal static IOrderedEnumerable<T> OrderByDescending<T>(this IEnumerable<T> source, IComparer<T>? comparer) {
        return source.OrderByDescending(Functions<T>.Identity, comparer);
    }

    internal static IOrderedEnumerable<T> OrderByDescending<T>(this IEnumerable<T> source, Comparison<T> compare) {
        return source.OrderByDescending(Comparer<T>.Create(compare));
    }
}
