using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class ArrayBuilderExtensions {
    // These methods allow using an array builder as a stack
    internal static void Push<T>(this ArrayBuilder<T> builder, T e) {
        builder.Add(e);
    }

    internal static T Pop<T>(this ArrayBuilder<T> builder) {
        var e = builder.Peek();
        builder.RemoveAt(builder.Count - 1);
        return e;
    }

    internal static T Peek<T>(this ArrayBuilder<T> builder) {
        return builder[^1];
    }
}
