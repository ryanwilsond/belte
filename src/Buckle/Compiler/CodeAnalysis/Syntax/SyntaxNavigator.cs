using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Navigates through a <see cref="SyntaxNode" /> recursively to find specific tokens.
/// </summary>
internal sealed class SyntaxNavigator {
    /// <summary>
    /// The single global instance.
    /// </summary>
    public static readonly SyntaxNavigator Instance = new SyntaxNavigator();

    private static readonly ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>> ChildReversedEnumeratorStackPool =
        new ObjectPool<Stack<ChildSyntaxList.Reversed.Enumerator>>(() =>
            new Stack<ChildSyntaxList.Reversed.Enumerator>(), 10);

    private static readonly ObjectPool<Stack<ChildSyntaxList.Enumerator>> ChildEnumeratorStackPool
        = new ObjectPool<Stack<ChildSyntaxList.Enumerator>>(() => new Stack<ChildSyntaxList.Enumerator>(), 10);

    private SyntaxNavigator() { }

    /// <summary>
    /// Gets the first <see cref="SyntaxToken" /> contained within the given node <param name="current" />.
    /// </summary>
    /// <param name="includeZeroWidth">If to include tokens with zero width in the search.</param>
    /// <param name="includeSkipped">If to include tokens marked as containing skipped text in the search.</param>
    internal SyntaxToken GetFirstToken(SyntaxNode current, bool includeZeroWidth, bool includeSkipped) {
        return GetFirstToken(current, GetPredicateFunction(includeZeroWidth), GetStepIntoFunction(includeSkipped));
    }

    /// <summary>
    /// Gets the last <see cref="SyntaxToken" /> contained within the given node <param name="current" />.
    /// </summary>
    /// <param name="includeZeroWidth">If to include tokens with zero width in the search.</param>
    /// <param name="includeSkipped">If to include tokens marked as containing skipped text in the search.</param>
    internal SyntaxToken GetLastToken(SyntaxNode current, bool includeZeroWidth, bool includeSkipped) {
        return GetLastToken(current, GetPredicateFunction(includeZeroWidth), GetStepIntoFunction(includeSkipped));
    }

    private SyntaxToken GetFirstToken(
        SyntaxNode current, Func<SyntaxToken, bool>? predicate, Func<SyntaxTrivia, bool>? stepInto) {
        var stack = ChildEnumeratorStackPool.Allocate();

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
            ChildEnumeratorStackPool.Free(stack);
        }
    }

    private SyntaxToken GetLastToken(
        SyntaxNode current, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        var stack = ChildReversedEnumeratorStackPool.Allocate();

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
            ChildReversedEnumeratorStackPool.Free(stack);
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
        return predicate is null || ReferenceEquals(predicate, SyntaxToken.Any) || predicate(token);
    }

    private static Func<SyntaxTrivia, bool> GetStepIntoFunction(bool skipped) {
        return skipped ? (t => t.kind == SyntaxKind.SkippedTokenTrivia) : null;
    }

    private static Func<SyntaxToken, bool> GetPredicateFunction(bool includeZeroWidth) {
        return includeZeroWidth ? SyntaxToken.Any : SyntaxToken.NonZeroWidth;
    }
}
