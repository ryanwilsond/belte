using System;
using System.Collections.Generic;

namespace Buckle.Utilities;

internal sealed class ReadOnlyMemoryOfCharComparer : IEqualityComparer<ReadOnlyMemory<char>> {
    internal static readonly ReadOnlyMemoryOfCharComparer Instance = new ReadOnlyMemoryOfCharComparer();

    private ReadOnlyMemoryOfCharComparer() {
    }

    public static bool Equals(ReadOnlySpan<char> x, ReadOnlyMemory<char> y)
        => x.SequenceEqual(y.Span);

    public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        => x.Span.SequenceEqual(y.Span);

    public int GetHashCode(ReadOnlyMemory<char> obj) {
        return string.GetHashCode(obj.Span);
    }
}
