using System;
using System.Runtime.CompilerServices;

namespace Belte.Runtime;

public static class ThrowHelper {
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullConditionException() {
        throw new NullConditionException();
    }

    public static void ThrowUnreachableException() {
        throw new InvalidOperationException("This program location is thought to be unreachable.");
    }

    public static void ThrowUnexpectedValueException(object value) {
        throw new InvalidOperationException($"Unexpected value '{value}'.");
    }
}
