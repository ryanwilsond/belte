using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal static class ImmutableArrayExtensions {
    internal static bool HasErrors<T>(this ImmutableArray<T> array) where T : BoundNode {
        foreach (var element in array) {
            if (element.HasErrors())
                return true;
        }

        return false;
    }

    internal static ImmutableArray<TResult> ZipAsArray<T1, T2, TArg, TResult>(
        this ImmutableArray<T1> self,
        ImmutableArray<T2> other,
        TArg arg,
        Func<T1, T2, int, TArg, TResult> map) {
        if (self.IsEmpty)
            return [];

        var builder = ArrayBuilder<TResult>.GetInstance(self.Length);

        for (var i = 0; i < self.Length; i++)
            builder.Add(map(self[i], other[i], i, arg));

        return builder.ToImmutableAndFree();
    }

    internal static bool SequenceEqual<TElement, TArg>(
        this ImmutableArray<TElement> array1,
        ImmutableArray<TElement> array2,
        TArg arg,
        Func<TElement, TElement, TArg, bool> predicate) {
        if (array1.IsDefault)
            throw new NullReferenceException();

        if (array2.IsDefault)
            throw new NullReferenceException();

        if (array1.Length != array2.Length)
            return false;

        for (var i = 0; i < array1.Length; i++) {
            if (!predicate(array1[i], array2[i], arg))
                return false;
        }

        return true;
    }
}
