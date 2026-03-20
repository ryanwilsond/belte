using System.Runtime.CompilerServices;

namespace Buckle.CodeAnalysis;

internal static unsafe class MCACUnsafe {
    /// <summary>
    /// Returns a by-ref to type <typeparamref name="T"/> that is a null reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T NullRef<T>() => ref Unsafe.AsRef<T>(null);

    /// <summary>
    /// Returns if a given by-ref to type <typeparamref name="T"/> is a null reference.
    /// </summary>
    /// <remarks>
    /// This check is conceptually similar to <c>(void*)(&amp;source) == nullptr</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNullRef<T>(ref T source) => Unsafe.AsPointer(ref source) is null;
}
