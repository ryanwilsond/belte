using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxNavigator {
    public static readonly SyntaxNavigator Instance = new SyntaxNavigator();

    private static readonly ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>> _childReversedEnumeratorStackPool =
        new ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>>(() =>
            new Stack<ChildSyntaxList.Reversed.Enumerator>(), 10);

    private static readonly ObjectPool<Stack<ChildSyntaxList.Enumerator>> _childEnumeratorStackPool
        = new ObjectPool<Stack<ChildSyntaxList.Enumerator>>(() => new Stack<ChildSyntaxList.Enumerator>(), 10);

    private SyntaxNavigator() { }

    internal SyntaxToken GetFirstToken(SyntaxNode current, bool includeZeroWidth, bool includeSkipped) {
        return GetFirstToken(current, GetPredicateFunction(includeZeroWidth), GetStepIntoFunction(includeSkipped));
    }

    internal SyntaxToken GetLastToken(SyntaxNode current, bool includeZeroWidth, bool includeSkipped) {
        return GetLastToken(current, GetPredicateFunction(includeZeroWidth), GetStepIntoFunction(includeSkipped));
    }

    internal SyntaxToken GetFirstToken(SyntaxNode current, Func<SyntaxToken, bool>? predicate, Func<SyntaxTrivia, bool>? stepInto) {
        var stack = _childEnumeratorStackPool.Allocate();

        try {
            stack.Push(current.ChildNodesAndTokens().GetEnumerator());

            while (stack.Count > 0) {
                var en = stack.Pop();

                if (en.MoveNext()) {
                    var child = en.Current;

                    if (child.isToken) {
                        var token = GetFirstToken(child.AsToken(), predicate, stepInto);

                        if (token != null)
                            return token;
                    }

                    stack.Push(en);

                    if (child.isNode)
                        stack.Push(child.AsNode()!.ChildNodesAndTokens().GetEnumerator());
                }
            }

            return null;
        } finally {
            stack.Clear();
            _childEnumeratorStackPool.Free(stack);
        }
    }

    internal SyntaxToken GetLastToken(
        SyntaxNode current, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        var stack = _childReversedEnumeratorStackPool.Allocate();

        try {
            stack.Push(current.ChildNodesAndTokens().Reverse().GetEnumerator());

            while (stack.Count > 0) {
                var en = stack.Pop();

                if (en.MoveNext()) {
                    var child = en.Current;

                    if (child.isToken) {
                        var token = GetLastToken(child.AsToken(), predicate, stepInto);

                        if (token != null)
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

    private SyntaxToken GetFirstToken(
            SyntaxToken token, Func<SyntaxToken, bool>? predicate, Func<SyntaxTrivia, bool>? stepInto) {
        if (stepInto != null) {
            var firstToken = GetFirstToken(token.leadingTrivia, predicate, stepInto);

            if (firstToken != null)
                return firstToken;
        }

        if (Matches(predicate, token))
            return token;

        if (stepInto != null) {
            var firstToken = GetFirstToken(token.trailingTrivia, predicate, stepInto);

            if (firstToken != null)
                return firstToken;
        }

        return null;
    }

    private SyntaxToken GetLastToken(
        SyntaxToken token, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        if (stepInto != null) {
            var lastToken = GetLastToken(token.trailingTrivia, predicate, stepInto);

            if (lastToken != null)
                return lastToken;
        }

        if (Matches(predicate, token))
            return token;

        if (stepInto != null) {
            var lastToken = GetLastToken(token.leadingTrivia, predicate, stepInto);

            if (lastToken != null)
                return lastToken;
        }

        return null;
    }

    private SyntaxToken GetFirstToken(
        SyntaxTriviaList list, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        throw new NotImplementedException();
    }

    private SyntaxToken GetLastToken(
        SyntaxTriviaList list, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        throw new NotImplementedException();
    }

    private static bool Matches(Func<SyntaxToken, bool>? predicate, SyntaxToken token) {
        return predicate == null || ReferenceEquals(predicate, SyntaxToken.Any) || predicate(token);
    }

    private static Func<SyntaxTrivia, bool> GetStepIntoFunction(bool skipped) {
        return skipped ? (t => t.kind == SyntaxKind.SkippedTokenTrivia) : null;
    }

    private static Func<SyntaxToken, bool> GetPredicateFunction(bool includeZeroWidth) {
        return includeZeroWidth ? SyntaxToken.Any : SyntaxToken.NonZeroWidth;
    }
}
