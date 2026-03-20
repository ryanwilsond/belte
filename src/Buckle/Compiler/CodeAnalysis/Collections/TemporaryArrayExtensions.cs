using System.Runtime.CompilerServices;

namespace Buckle.CodeAnalysis;

internal static class TemporaryArrayExtensions {
    public static ref TemporaryArray<T> AsRef<T>(this in TemporaryArray<T> array)
        => ref Unsafe.AsRef(in array);
}
