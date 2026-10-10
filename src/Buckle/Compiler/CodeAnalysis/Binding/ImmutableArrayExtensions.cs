using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal static class ImmutableArrayExtensions {
    internal static bool HasErrors<T>(this ImmutableArray<T> array) where T : BoundNode {
        foreach (var element in array) {
            if (element.HasErrors())
                return true;
        }

        return false;
    }
}
