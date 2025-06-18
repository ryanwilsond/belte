using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static class CollectionExtensions {
    internal static bool IsNullOrEmpty<T>(this ICollection<T>? collection) {
        return collection is null || collection.Count == 0;
    }
}
