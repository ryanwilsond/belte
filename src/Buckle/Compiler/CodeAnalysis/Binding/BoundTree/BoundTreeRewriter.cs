using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeRewriter : BoundTreeVisitor {
    internal virtual TypeSymbol VisitType(TypeSymbol? type) {
        return type;
    }

    internal ImmutableArray<T> VisitList<T>(ImmutableArray<T> list) where T : BoundNode {
        if (list.IsDefault)
            return list;

        return DoVisitList(list);
    }

    private ImmutableArray<T> DoVisitList<T>(ImmutableArray<T> list) where T : BoundNode {
        ArrayBuilder<T>? newList = null;

        for (var i = 0; i < list.Length; i++) {
            var item = list[i];
            System.Diagnostics.Debug.Assert(item != null);

            var visited = Visit(item);

            if (newList is null && item != visited) {
                newList = ArrayBuilder<T>.GetInstance();

                if (i > 0)
                    newList.AddRange(list, i);
            }

            if (newList is not null && visited is not null)
                newList.Add((T)visited);
        }

        if (newList is not null)
            return newList.ToImmutableAndFree();

        return list;
    }
}
