using System.Runtime.CompilerServices;

namespace Buckle.Utilities;

internal static class StackGuard {
    internal const int MaxUncheckedRecursionDepth = 20;

    internal static void EnsureSufficientExecutionStack(int recursionDepth) {
        if (recursionDepth > MaxUncheckedRecursionDepth)
            RuntimeHelpers.EnsureSufficientExecutionStack();
    }
}
