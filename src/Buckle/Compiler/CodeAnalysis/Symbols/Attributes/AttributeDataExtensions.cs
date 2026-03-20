using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal static class AttributeDataExtensions {
    internal static int IndexOfAttribute(
        this ImmutableArray<AttributeData> attributes,
        AttributeDescription description) {
        for (var i = 0; i < attributes.Length; i++) {
            if (attributes[i].IsTargetAttribute(description))
                return i;
        }

        return -1;
    }
}
