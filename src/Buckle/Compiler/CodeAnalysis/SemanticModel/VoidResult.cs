using System;

namespace Buckle.CodeAnalysis;

internal readonly struct VoidResult : IEquatable<VoidResult> {
    public override bool Equals(object? obj) {
        return obj is VoidResult;
    }

    public override int GetHashCode() {
        return 0;
    }

    public bool Equals(VoidResult other) {
        return true;
    }
}
