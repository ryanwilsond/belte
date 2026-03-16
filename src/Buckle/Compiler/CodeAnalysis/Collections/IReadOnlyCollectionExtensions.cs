using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Buckle.CodeAnalysis;

internal static class IReadOnlyCollectionExtensions {
    internal static ImmutableArray<TResult> SelectAsArray<TSource, TResult>(
        this IReadOnlyCollection<TSource> source,
        Func<TSource, TResult> selector) {
        if (source is null)
            return [];

        var builder = new TResult[source.Count];
        var index = 0;

        foreach (var item in source) {
            builder[index] = selector(item);
            index++;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(builder);
    }
}
