using System.Runtime.CompilerServices;

namespace Buckle.CodeAnalysis;

internal static class TemporaryArrayExtensions {
    public static ref TemporaryArray<T> AsRef<T>(this in TemporaryArray<T> array)
        => ref Unsafe.AsRef(in array);

    internal static void AddIfNotNull<T>(this ref TemporaryArray<T> array, T? value)
        where T : class {

        if (value is not null)
            array.Add(value);
    }
}
