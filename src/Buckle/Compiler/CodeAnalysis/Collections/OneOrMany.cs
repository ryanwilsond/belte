using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal static class OneOrMany {
    internal static OneOrMany<T> Create<T>(T one) {
        return new OneOrMany<T>(one);
    }

    internal static OneOrMany<T> Create<T>(T one, T two) {
        return new OneOrMany<T>([one, two]);
    }

    internal static OneOrMany<T> OneOrNone<T>(T? one) {
        return one is null ? OneOrMany<T>.Empty : new OneOrMany<T>(one);
    }

    internal static OneOrMany<T> Create<T>(ImmutableArray<T> many)
        => new OneOrMany<T>(many);

    internal static bool SequenceEqual<T>(
        this ImmutableArray<T> array,
        OneOrMany<T> other,
        IEqualityComparer<T> comparer = null) {
        return Create(array).SequenceEqual(other, comparer);
    }

    internal static bool SequenceEqual<T>(
        this IEnumerable<T> array,
        OneOrMany<T> other,
        IEqualityComparer<T> comparer = null) {
        return other.SequenceEqual(array, comparer);
    }
}
