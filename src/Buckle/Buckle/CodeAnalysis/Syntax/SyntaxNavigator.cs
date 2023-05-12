using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxNavigator {
    public static readonly SyntaxNavigator Instance = new SyntaxNavigator();

    private static readonly ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>> _childReversedEnumeratorStackPool =
        new ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>>(() =>
            new Stack<ChildSyntaxList.Reversed.Enumerator>(), 10);

    private SyntaxNavigator() { }

    internal SyntaxToken GetLastToken(SyntaxNode current) {
        var stack = _childReversedEnumeratorStackPool.Allocate();

        try {
            stack.Push(current.ChildNodesAndTokens().Reverse().GetEnumerator());

            while (stack.Count > 0) {
                var en = stack.Pop();

                if (en.MoveNext()) {
                    var child = en.Current;

                    if (child.isToken) {
                        var token = GetLastToken(child.AsToken());

                        if (token.kind != SyntaxKind.None)
                            return token;
                    }

                    stack.Push(en);

                    if (child.isNode)
                        stack.Push(child.AsNode().ChildNodesAndTokens().Reverse().GetEnumerator());
                }
            }

            return null;
        } finally {
            stack.Clear();
            _childReversedEnumeratorStackPool.Free(stack);
        }
    }

    private SyntaxToken GetLastToken(SyntaxToken token) {
        if (Matches(token))
            return token;

        return null;
    }

    private static bool Matches(SyntaxToken token) {
        // Could implement predicate and stepInto functions later if needed
        // Currently this is just the default "NonZeroWidth" predicate hard-coded
        return token.width > 0;
    }
}
