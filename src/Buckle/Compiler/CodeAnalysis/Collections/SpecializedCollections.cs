using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    internal static IEnumerable<T> EmptyEnumerable<T>() {
        return Empty.List<T>.Instance;
    }

    internal static IEnumerator<T> EmptyEnumerator<T>() {
        return Empty.Enumerator<T>.Instance;
    }

    internal static ICollection<T> EmptyCollection<T>() {
        return Empty.List<T>.Instance;
    }

    internal static IReadOnlySet<T> EmptyReadOnlySet<T>() {
        return Empty.Set<T>.Instance;
    }

    public static IList<T> EmptyList<T>() {
        return Empty.List<T>.Instance;
    }

    internal static ISet<T> EmptySet<T>() {
        return Empty.Set<T>.Instance;
    }

    internal static IDictionary<TKey, TValue> EmptyDictionary<TKey, TValue>()
        where TKey : notnull {
        return Empty.Dictionary<TKey, TValue>.Instance;
    }

    internal static IEnumerable<T> SingletonEnumerable<T>(T value) {
        return new Singleton.List<T>(value);
    }

    internal static ICollection<T> SingletonCollection<T>(T value) {
        return new Singleton.List<T>(value);
    }

    internal static ISet<T> ReadOnlySet<T>(ISet<T>? set) {
        return set is null || set.Count == 0
            ? EmptySet<T>()
            : new ReadOnly.Set<ISet<T>, T>(set);
    }

    internal static ICollection<T> ReadOnlyCollection<T>(ICollection<T>? collection) {
        return collection is null || collection.Count == 0
            ? EmptyCollection<T>()
            : [.. collection];
    }
}
