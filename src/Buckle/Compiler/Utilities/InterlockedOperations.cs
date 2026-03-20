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

    private static T GetOrStore<T>([NotNull] ref T target, T value) where T : class
        => Interlocked.CompareExchange(ref target, value, null) ?? value;
}
