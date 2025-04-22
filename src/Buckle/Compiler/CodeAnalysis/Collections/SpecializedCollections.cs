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

    internal static ISet<T> EmptySet<T>() {
        return Empty.Set<T>.Instance;
    }

    internal static IEnumerable<T> SingletonEnumerable<T>(T value) {
        return new Singleton.List<T>(value);
    }

    internal static ISet<T> ReadOnlySet<T>(ISet<T>? set) {
        return set == null || set.Count == 0
            ? EmptySet<T>()
            : new ReadOnly.Set<ISet<T>, T>(set);
    }
}
