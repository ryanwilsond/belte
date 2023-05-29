using System.Collections.Generic;

namespace Buckle.Utilities;

internal sealed class StringOrdinalComparer : IEqualityComparer<string> {
    public static readonly StringOrdinalComparer Instance = new StringOrdinalComparer();

    private StringOrdinalComparer() { }

    bool IEqualityComparer<string>.Equals(string? a, string? b) {
        return StringOrdinalComparer.Equals(a, b);
    }

    public static bool Equals(string? a, string? b) {
        return string.Equals(a, b);
    }

    int IEqualityComparer<string>.GetHashCode(string s) {
        return Hash.GetFNVHashCode(s);
    }
}
