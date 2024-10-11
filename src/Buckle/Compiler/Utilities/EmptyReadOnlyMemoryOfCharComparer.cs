using System;
using System.Collections.Generic;

namespace Buckle.Utilities;

internal sealed class EmptyReadOnlyMemoryOfCharComparer : IEqualityComparer<ReadOnlyMemory<char>> {
    public static readonly EmptyReadOnlyMemoryOfCharComparer Instance = new EmptyReadOnlyMemoryOfCharComparer();

    private EmptyReadOnlyMemoryOfCharComparer() { }

    public bool Equals(ReadOnlyMemory<char> a, ReadOnlyMemory<char> b)
        => throw ExceptionUtilities.Unreachable();

    public int GetHashCode(ReadOnlyMemory<char> s) {
        return 0;
    }
}
