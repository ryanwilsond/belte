using System.Runtime.CompilerServices;

namespace Belte.Runtime;

public static class ThrowHelper {
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullConditionException() {
        throw new NullConditionException();
    }
}
