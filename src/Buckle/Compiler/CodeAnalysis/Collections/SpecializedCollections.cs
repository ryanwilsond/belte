using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    internal static IEnumerable<T> EmptyEnumerable<T>() {
        return Empty.List<T>.Instance;
    }
}
