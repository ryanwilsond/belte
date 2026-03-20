using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class ArrayBuilderExtensions {
    // These methods allow using an array builder as a stack
    internal static void Push<T>(this ArrayBuilder<T> builder, T e) {
        builder.Add(e);
    }

    internal static T Pop<T>(this ArrayBuilder<T> builder) {
        var e = builder.Peek();
        builder.RemoveAt(builder.Count - 1);
        return e;
    }

    internal static bool TryPop<T>(this ArrayBuilder<T> builder, out T result) {
        if (builder.Count > 0) {
            result = builder.Pop();
            return true;
        }

        result = default;
        return false;
    }

    internal static T Peek<T>(this ArrayBuilder<T> builder) {
        return builder[^1];
    }

    internal static bool All<T>(this ArrayBuilder<T> builder, Func<T, bool> predicate) {
        foreach (var item in builder) {
            if (!predicate(item))
                return false;
        }

        return true;
    }

    internal static void AddIfNotNull<T>(this ArrayBuilder<T> builder, T value) where T : class {
        if (value is not null)
            builder.Add(value);
    }

    internal static OneOrMany<T> ToOneOrManyAndFree<T>(this ArrayBuilder<T> builder) {
        if (builder.Count == 1) {
            var result = OneOrMany.Create(builder[0]);
            builder.Free();
            return result;
        } else {
            return OneOrMany.Create(builder.ToImmutableAndFree());
        }
    }

    internal static ImmutableArray<TResult> SelectAsArray<TItem, TResult>(
        this ArrayBuilder<TItem> items,
        Func<TItem, TResult> map) {
        switch (items.Count) {
            case 0:
                return [];
            case 1:
                return [map(items[0])];
            case 2:
                return [map(items[0]), map(items[1])];
            case 3:
                return [map(items[0]), map(items[1]), map(items[2])];
            case 4:
                return [map(items[0]), map(items[1]), map(items[2]), map(items[3])];
            default:
                var builder = ArrayBuilder<TResult>.GetInstance(items.Count);

                foreach (var item in items)
                    builder.Add(map(item));

                return builder.ToImmutableAndFree();
        }
    }

    internal static ImmutableArray<U> ToDowncastedImmutableAndFree<T, U>(this ArrayBuilder<T> builder) where U : T {
        var result = builder.ToDowncastedImmutable<U>();
        builder.Free();
        return result;
    }
}
