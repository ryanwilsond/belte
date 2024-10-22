using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstantEvaluationHelpers {
    private struct Node<T> where T : class {
        internal ImmutableHashSet<T> dependencies;

        internal ImmutableHashSet<T> dependedOnBy;
    }
}
