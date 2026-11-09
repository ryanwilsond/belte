using System;
using System.Runtime.CompilerServices;

namespace Belte.Runtime;

public static class ThrowHelper {
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullConditionException() {
        throw new NullConditionException();
    }

    public static InvalidOperationException GetUnreachableException() {
        return new InvalidOperationException("This program location is thought to be unreachable.");
    }

    public static InvalidOperationException GetUnexpectedValueException(object value) {
        return new InvalidOperationException($"Unexpected value '{value}'.");
    }
}
