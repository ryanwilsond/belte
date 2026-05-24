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

    internal SyntaxToken GetNextToken(SyntaxToken current, bool includeZeroWidth, bool includeSkipped) {
        return GetNextToken(current, GetPredicateFunction(includeZeroWidth), GetStepIntoFunction(includeSkipped));
    }

    internal SyntaxToken GetPreviousToken(SyntaxToken current, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        return GetPreviousToken(current, predicate, stepInto is not null, stepInto);
    }

    internal SyntaxToken GetPreviousToken(
        SyntaxToken current,
        bool includeZeroWidth,
        bool includeSkipped,
        bool includeDirectives) {
        return GetPreviousToken(
            current,
            GetPredicateFunction(includeZeroWidth),
            // TODO Directives
            // GetStepIntoFunction(includeSkipped, includeDirectives)
            GetStepIntoFunction(includeSkipped)
        );
    }

    internal SyntaxToken GetNextToken(
        SyntaxToken current,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        return GetNextToken(current, predicate, stepInto is not null, stepInto);
    }

    internal SyntaxToken GetNextToken(
        SyntaxToken current,
        Func<SyntaxToken, bool> predicate,
        bool searchInsideCurrentTokenTrailingTrivia,
        Func<SyntaxTrivia, bool> stepInto) {
        if (current.parent is not null) {
            if (searchInsideCurrentTokenTrailingTrivia) {
                var firstToken = GetFirstToken(current.trailingTrivia, predicate, stepInto!);

                if (firstToken is not null)
                    return firstToken;
            }

            var returnNext = false;

            foreach (var child in current.parent.ChildNodesAndTokens()) {
                if (returnNext) {
                    if (child.isToken) {
                        var token = GetFirstToken(child.AsToken(), predicate, stepInto);

                        if (token is not null)
                            return token;
                    } else {
                        var token = GetFirstToken(child.AsNode()!, predicate, stepInto);

                        if (token is not null)
                            return token;
                    }
                } else if (child.isToken && child.AsToken() == current) {
                    returnNext = true;
                }
            }

            return GetNextToken(current.parent, predicate, stepInto);
        }

        return default;
    }

    internal SyntaxToken GetNextToken(
        SyntaxNode node,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        while (node.parent is not null) {
            var returnNext = false;

            foreach (var child in node.parent.ChildNodesAndTokens()) {
                if (returnNext) {
                    if (child.isToken) {
                        var token = GetFirstToken(child.AsToken(), predicate, stepInto);

                        if (token is not null)
                            return token;
                    } else {
                        var token = GetFirstToken(child.AsNode()!, predicate, stepInto);

                        if (token is not null)
                            return token;
                    }
                } else if (child.isNode && child.AsNode() == node) {
                    returnNext = true;
                }
            }

            node = node.parent;
        }

        return default;
    }

    private SyntaxToken GetFirstToken(
        SyntaxNode current,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        var stack = ChildEnumeratorStackPool.Allocate();

        try {
            stack.Push(current.ChildNodesAndTokens().GetEnumerator());

            while (stack.Count > 0) {
                var en = stack.Pop();

                if (en.MoveNext()) {
                    var child = en.Current;

                    if (child.isToken) {
                        var token = GetFirstToken(child.AsToken(), predicate, stepInto);

                        if (token is not null)
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
        SyntaxNode current,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        var stack = ChildReversedEnumeratorStackPool.Allocate();

        try {
            stack.Push(current.ChildNodesAndTokens().Reverse().GetEnumerator());

            while (stack.Count > 0) {
                var en = stack.Pop();

                if (en.MoveNext()) {
                    var child = en.Current;

                    if (child.isToken) {
                        var token = GetLastToken(child.AsToken(), predicate, stepInto);

                        if (token is not null)
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

    internal SyntaxToken GetPreviousToken(
        SyntaxToken current,
        Func<SyntaxToken, bool> predicate,
        bool searchInsideCurrentTokenLeadingTrivia,
        Func<SyntaxTrivia, bool> stepInto) {
        if (current.parent is not null) {
            if (searchInsideCurrentTokenLeadingTrivia) {
                var lastToken = GetLastToken(current.leadingTrivia, predicate, stepInto!);

                if (lastToken.kind != SyntaxKind.None)
                    return lastToken;
            }

            var returnPrevious = false;

            foreach (var child in current.parent.ChildNodesAndTokens().Reverse()) {
                if (returnPrevious) {
                    if (child.isToken) {
                        var token = GetLastToken(child.AsToken(), predicate, stepInto);

                        if (token.kind != SyntaxKind.None)
                            return token;
                    } else {
                        var token = GetLastToken(child.AsNode(), predicate, stepInto);

                        if (token.kind != SyntaxKind.None)
                            return token;
                    }
                } else if (child.isToken && child.AsToken() == current) {
                    returnPrevious = true;
                }
            }

            return GetPreviousToken(current.parent, predicate, stepInto);
        }

        return default;
    }

    internal SyntaxToken GetPreviousToken(
        SyntaxNode node,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        while (node.parent is not null) {
            var returnPrevious = false;

            foreach (var child in node.parent.ChildNodesAndTokens().Reverse()) {
                if (returnPrevious) {
                    if (child.isToken) {
                        var token = GetLastToken(child.AsToken(), predicate, stepInto);

                        if (token.kind != SyntaxKind.None)
                            return token;
                    } else {
                        var token = GetLastToken(child.AsNode(), predicate, stepInto);

                        if (token.kind != SyntaxKind.None)
                            return token;
                    }
                } else if (child.isNode && child.AsNode() == node) {
                    returnPrevious = true;
                }
            }

            node = node.parent;
        }

        if (node is StructuredTriviaSyntax s)
            return GetPreviousToken(s.parentTrivia, predicate, stepInto);

        return default;
    }

    internal SyntaxToken GetPreviousToken(
        SyntaxTrivia current,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool> stepInto) {
        var returnPrevious = false;

        var token = GetPreviousToken(current, current.token.trailingTrivia, predicate, stepInto, ref returnPrevious);

        if (token.kind != SyntaxKind.None)
            return token;

        if (returnPrevious && Matches(predicate, current.token))
            return current.token;

        token = GetPreviousToken(current, current.token.leadingTrivia, predicate, stepInto, ref returnPrevious);

        if (token.kind != SyntaxKind.None)
            return token;

        return GetPreviousToken(current.token, predicate, false, stepInto);
    }

    private SyntaxToken GetPreviousToken(
        SyntaxTrivia current,
        SyntaxTriviaList list,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool>? stepInto,
        ref bool returnPrevious) {
        foreach (var trivia in list.Reverse()) {
            if (returnPrevious) {
                if (TryGetLastTokenForStructuredTrivia(trivia, predicate, stepInto, out var token))
                    return token;
            } else if (trivia == current) {
                returnPrevious = true;
            }
        }

        return default;
    }

    private bool TryGetLastTokenForStructuredTrivia(
        SyntaxTrivia trivia,
        Func<SyntaxToken, bool> predicate,
        Func<SyntaxTrivia, bool>? stepInto,
        out SyntaxToken token) {
        token = default;

        if (!trivia.TryGetStructure(out var structure) || stepInto is null || !stepInto(trivia))
            return false;

        token = GetLastToken(structure, predicate, stepInto);

        return token.kind != SyntaxKind.None;
    }

    private SyntaxToken GetFirstToken(
            SyntaxToken token, Func<SyntaxToken, bool>? predicate, Func<SyntaxTrivia, bool>? stepInto) {
        if (stepInto is not null) {
            var firstToken = GetFirstToken(token.leadingTrivia, predicate, stepInto);

            if (firstToken is not null)
                return firstToken;
        }

        if (Matches(predicate, token))
            return token;

        if (stepInto is not null) {
            var firstToken = GetFirstToken(token.trailingTrivia, predicate, stepInto);

            if (firstToken is not null)
                return firstToken;
        }

        return null;
    }

    private SyntaxToken GetLastToken(
        SyntaxToken token, Func<SyntaxToken, bool> predicate, Func<SyntaxTrivia, bool> stepInto) {
        if (stepInto is not null) {
            var lastToken = GetLastToken(token.trailingTrivia, predicate, stepInto);

            if (lastToken is not null)
                return lastToken;
        }

        if (Matches(predicate, token))
            return token;

        if (stepInto is not null) {
            var lastToken = GetLastToken(token.leadingTrivia, predicate, stepInto);

            if (lastToken is not null)
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
        return skipped ? (t => t.kind == SyntaxKind.SkippedTokensTrivia) : null;
    }

    private static Func<SyntaxToken, bool> GetPredicateFunction(bool includeZeroWidth) {
        return includeZeroWidth ? SyntaxToken.Any : SyntaxToken.NonZeroWidth;
    }
}
