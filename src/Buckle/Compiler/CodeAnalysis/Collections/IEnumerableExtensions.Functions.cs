using System;

namespace Buckle.CodeAnalysis;

internal static partial class IEnumerableExtensions {
    internal static class Functions<T> {
        internal static readonly Func<T, T> Identity = t => t;
        internal static readonly Func<T, bool> True = t => true;
    }
}
