using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class SyntaxNode {
    internal IEnumerable<SyntaxNode> DescendantNodes(
        Func<SyntaxNode, bool> descendIntoChildren = null,
        bool descendIntoTrivia = false) {
        return DescendantNodesImpl(fullSpan, descendIntoChildren, descendIntoTrivia, includeSelf: false);
    }

    internal IEnumerable<SyntaxNode> DescendantNodesAndSelf(
        Func<SyntaxNode, bool> descendIntoChildren = null,
        bool descendIntoTrivia = false) {
        return DescendantNodesImpl(fullSpan, descendIntoChildren, descendIntoTrivia, includeSelf: true);
    }

    private static bool IsInSpan(in TextSpan span, TextSpan childSpan) {
        return span.OverlapsWith(childSpan) || (childSpan.length == 0 && span.IntersectsWith(childSpan));
    }

    private IEnumerable<SyntaxNode> DescendantNodesImpl(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren,
        bool descendIntoTrivia,
        bool includeSelf) {
        return descendIntoTrivia
            ? DescendantNodesAndTokensImpl(span, descendIntoChildren, true, includeSelf)
                .Where(e => e.isNode).Select(e => e.AsNode())
            : DescendantNodesOnly(span, descendIntoChildren, includeSelf);
    }

    private IEnumerable<SyntaxNodeOrToken> DescendantNodesAndTokensImpl(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren,
        bool descendIntoTrivia,
        bool includeSelf) {
        return descendIntoTrivia
            ? DescendantNodesAndTokensIntoTrivia(span, descendIntoChildren, includeSelf)
            : DescendantNodesAndTokensOnly(span, descendIntoChildren, includeSelf);
    }

    private IEnumerable<SyntaxNode> DescendantNodesOnly(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren,
        bool includeSelf) {
        if (includeSelf && IsInSpan(in span, fullSpan))
            yield return this;

        using var stack = new ChildSyntaxListEnumeratorStack(this, descendIntoChildren);
        while (stack.isNotEmpty) {
            var nodeValue = stack.TryGetNextAsNodeInSpan(in span);

            if (nodeValue is not null) {
                stack.PushChildren(nodeValue, descendIntoChildren);
                yield return nodeValue;
            }
        }
    }

    private IEnumerable<SyntaxNodeOrToken> DescendantNodesAndTokensOnly(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren,
        bool includeSelf) {
        if (includeSelf && IsInSpan(in span, fullSpan))
            yield return this;

        using var stack = new ChildSyntaxListEnumeratorStack(this, descendIntoChildren);
        while (stack.isNotEmpty) {
            if (stack.TryGetNextInSpan(in span, out var value)) {
                var nodeValue = value.AsNode();

                if (nodeValue is not null)
                    stack.PushChildren(nodeValue, descendIntoChildren);

                yield return value;
            }
        }
    }

    private IEnumerable<SyntaxNodeOrToken> DescendantNodesAndTokensIntoTrivia(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren,
        bool includeSelf) {
        // ? Gaps are from where structured trivia is handled (which we don't have)

        if (includeSelf && IsInSpan(in span, fullSpan))
            yield return this;

        using var stack = new ThreeEnumeratorListStack(this, descendIntoChildren);
        while (stack.isNotEmpty) {
            switch (stack.PeekNext()) {
                case ThreeEnumeratorListStack.Which.Node:
                    if (stack.TryGetNextInSpan(in span, out var value)) {
                        if (value.isNode) {
                            stack.PushChildren(value.AsNode()!, descendIntoChildren);
                        } else if (value.isToken) {
                            var token = value.AsToken();
                        }

                        yield return value;
                    }

                    break;

                case ThreeEnumeratorListStack.Which.Trivia:
                    if (stack.TryGetNext(out var trivia)) {

                    }
                    break;

                case ThreeEnumeratorListStack.Which.Token:
                    yield return stack.PopToken();
                    break;
            }
        }
    }

    internal IEnumerable<SyntaxTrivia> DescendantTrivia(
        Func<SyntaxNode, bool> descendIntoChildren = null,
        bool descendIntoTrivia = false) {
        return descendIntoTrivia
            ? DescendantTriviaIntoTrivia(fullSpan, descendIntoChildren)
            : DescendantTriviaOnly(fullSpan, descendIntoChildren);
    }

    private IEnumerable<SyntaxTrivia> DescendantTriviaIntoTrivia(
        TextSpan span,
        Func<SyntaxNode, bool> descendIntoChildren) {
        using var stack = new TwoEnumeratorListStack(this, descendIntoChildren);
        while (stack.isNotEmpty) {
            switch (stack.PeekNext()) {
                case TwoEnumeratorListStack.Which.Node:
                    if (stack.TryGetNextInSpan(in span, out var value)) {
                        if (value.AsNode(out var nodeValue)) {
                            stack.PushChildren(nodeValue, descendIntoChildren);
                        } else if (value.isToken) {
                            var token = value.AsToken();

                            if (token.hasTrailingTrivia)
                                stack.PushTrailingTrivia(in token);

                            if (token.hasLeadingTrivia)
                                stack.PushLeadingTrivia(in token);
                        }
                    }

                    break;

                case TwoEnumeratorListStack.Which.Trivia:
                    if (stack.TryGetNext(out var trivia)) {
                        if (trivia.TryGetStructure(out var structureNode))
                            stack.PushChildren(structureNode, descendIntoChildren);

                        if (IsInSpan(in span, trivia.fullSpan))
                            yield return trivia;
                    }

                    break;
            }
        }
    }

    private IEnumerable<SyntaxTrivia> DescendantTriviaOnly(TextSpan span, Func<SyntaxNode, bool> descendIntoChildren) {
        using var stack = new ChildSyntaxListEnumeratorStack(this, descendIntoChildren);
        while (stack.isNotEmpty) {
            if (stack.TryGetNextInSpan(in span, out var value)) {
                if (value.AsNode(out var nodeValue)) {
                    stack.PushChildren(nodeValue, descendIntoChildren);
                } else if (value.isToken) {
                    var token = value.AsToken();

                    foreach (var trivia in token.leadingTrivia) {
                        if (IsInSpan(in span, trivia.fullSpan))
                            yield return trivia;
                    }

                    foreach (var trivia in token.trailingTrivia) {
                        if (IsInSpan(in span, trivia.fullSpan))
                            yield return trivia;
                    }
                }
            }
        }
    }
}
