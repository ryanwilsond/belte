using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal static class ImmutableHashSetExtensions {
    internal static bool SetEqualsWithoutIntermediateHashSet<T>(
        this ImmutableHashSet<T> set,
        ImmutableHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(set, other))
            return true;

        var otherSet = other.WithComparer(set.KeyComparer);
        if (set.Count != otherSet.Count)
            return false;

        foreach (var item in other) {
            if (!set.Contains(item))
                return false;
        }

        return true;
    }
}
