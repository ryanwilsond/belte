using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static class IEnumerableExtensions {
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
}
