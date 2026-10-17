using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Buckle.Utilities;

internal static class InterlockedOperations {
    internal static T Initialize<T>([NotNull] ref T target, T value) where T : class {
        return GetOrStore(ref target, value);
    }

    internal static ImmutableArray<T> Initialize<T>(ref ImmutableArray<T> target, ImmutableArray<T> initializedValue) {
        var oldValue = ImmutableInterlocked.InterlockedCompareExchange(ref target, initializedValue, default);
        return oldValue.IsDefault ? initializedValue : oldValue;
    }

    internal static T Initialize<T, TArg>([NotNull] ref T? target, Func<TArg, T> valueFactory, TArg arg)
        where T : class {
        return Volatile.Read(ref target) ?? GetOrStore(ref target, valueFactory(arg));
    }

    private static T GetOrStore<T>([NotNull] ref T target, T value) where T : class
        => Interlocked.CompareExchange(ref target, value, null) ?? value;
}
