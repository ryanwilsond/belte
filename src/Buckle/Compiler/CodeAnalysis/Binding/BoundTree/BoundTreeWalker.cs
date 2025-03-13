using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeWalker : BoundTreeVisitor {
    private protected BoundTreeWalker() { }

    internal void VisitList<T>(ImmutableArray<T> list) where T : BoundNode {
        if (!list.IsDefault) {
            for (var i = 0; i < list.Length; i++)
                Visit(list[i]);
        }
    }
}
