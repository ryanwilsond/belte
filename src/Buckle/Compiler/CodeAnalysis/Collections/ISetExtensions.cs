using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static class ISetExtensions {
    internal static bool AddAll<T>(this ISet<T> set, IEnumerable<T> values) {
        var result = false;

        foreach (var v in values)
            result |= set.Add(v);

        return result;
    }
}
