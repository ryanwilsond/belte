using System;
using System.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract partial class SyntaxRewriter : SyntaxVisitor<SyntaxNode> {
    private readonly bool _visitIntoStructuredTrivia;

    internal SyntaxRewriter(bool visitIntoStructuredTrivia = false) {
        _visitIntoStructuredTrivia = visitIntoStructuredTrivia;
    }

    internal virtual bool visitIntoStructuredTrivia => _visitIntoStructuredTrivia;

    private int _recursionDepth;

    internal override SyntaxNode Visit(SyntaxNode node) {
        if (node is not null) {
            _recursionDepth++;
            StackGuard.EnsureSufficientExecutionStack(_recursionDepth);

            var result = ((BelteSyntaxNode)node).Accept(this);

            _recursionDepth--;
            return result;
        } else {
            return null;
        }
    }

    internal virtual SyntaxToken VisitToken(SyntaxToken token) {
        var node = token.node;

        if (node is null)
            return token;

        var leadingTrivia = node.GetLeadingTrivia();
        var trailingTrivia = node.GetTrailingTrivia();

        Debug.Assert(leadingTrivia is null || !leadingTrivia.isList || leadingTrivia.slotCount > 0);
        Debug.Assert(trailingTrivia is null || !trailingTrivia.isList || trailingTrivia.slotCount > 0);

        if (leadingTrivia is not null) {
            var leading = VisitList(new SyntaxTriviaList(token, leadingTrivia, token.position));

            if (trailingTrivia is not null) {
                var index = leadingTrivia.isList ? leadingTrivia.slotCount : 1;

                var trailing = VisitList(
                    new SyntaxTriviaList(
                        token,
                        trailingTrivia,
                        token.position + node.fullWidth - trailingTrivia.fullWidth,
                        index
                    )
                );

                if (leading.node != leadingTrivia)
                    token = token.WithLeadingTrivia(leading);

                return trailing.node != trailingTrivia ? token.WithTrailingTrivia(trailing) : token;
            } else {
                return leading.node != leadingTrivia ? token.WithLeadingTrivia(leading) : token;
            }
        } else if (trailingTrivia is not null) {
            var trailing = VisitList(new SyntaxTriviaList(
                token,
                trailingTrivia,
                token.position + node.fullWidth - trailingTrivia.fullWidth,
                index: 0
            ));

            return trailing.node != trailingTrivia ? token.WithTrailingTrivia(trailing) : token;
        } else {
            return token;
        }
    }

    internal virtual SyntaxTrivia VisitTrivia(SyntaxTrivia trivia) {
        if (visitIntoStructuredTrivia && trivia.hasStructure) {
            var structure = (BelteSyntaxNode)trivia.GetStructure();
            var newStructure = (StructuredTriviaSyntax)Visit(structure);

            if (newStructure != structure) {
                if (newStructure is not null)
                    return new SyntaxTrivia(default, newStructure.green, 0, 0);
                else
                    return default;
            }
        }

        return trivia;
    }

    internal virtual SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list) where TNode : SyntaxNode {
        SyntaxListBuilder<TNode> alternate = default;
        for (int i = 0, n = list.Count; i < n; i++) {
            var item = list[i];
            var visited = VisitListElement(item);

            if (item != visited && alternate.IsNull()) {
                alternate = new SyntaxListBuilder<TNode>(n);
                alternate.AddRange(list, 0, i);
            }

            if (!alternate.IsNull() && visited is not null && visited.kind != SyntaxKind.None)
                alternate.Add(visited);
        }

        if (!alternate.IsNull())
            return alternate.ToList();

        return list;
    }

    internal virtual TNode VisitListElement<TNode>(TNode node) where TNode : SyntaxNode {
        return (TNode)Visit(node);
    }

    internal virtual SeparatedSyntaxList<TNode> VisitList<TNode>(SeparatedSyntaxList<TNode> list) where TNode : SyntaxNode {
        var count = list.Count;
        var sepCount = list.SeparatorCount;

        SeparatedSyntaxListBuilder<TNode> alternate = default;

        var i = 0;
        for (; i < sepCount; i++) {
            var node = list[i];
            var visitedNode = VisitListElement(node);

            var separator = list.GetSeparator(i);
            var visitedSeparator = VisitListSeparator(separator);

            if (alternate.IsNull()) {
                if (node != visitedNode || separator != visitedSeparator) {
                    alternate = new SeparatedSyntaxListBuilder<TNode>(count);
                    alternate.AddRange(list, i);
                }
            }

            if (!alternate.IsNull()) {
                if (visitedNode is not null) {
                    alternate.Add(visitedNode);

                    if (visitedSeparator.kind == 0)
                        throw new InvalidOperationException("SeparatorIsExpected");

                    alternate.AddSeparator(visitedSeparator);
                } else {
                    if (visitedNode is null)
                        throw new InvalidOperationException("ElementIsExpected");
                }
            }
        }

        if (i < count) {
            var node = list[i];
            var visitedNode = VisitListElement(node);

            if (alternate.IsNull()) {
                if (node != visitedNode) {
                    alternate = new SeparatedSyntaxListBuilder<TNode>(count);
                    alternate.AddRange(list, i);
                }
            }

            if (!alternate.IsNull() && visitedNode is not null)
                alternate.Add(visitedNode);
        }

        if (!alternate.IsNull())
            return alternate.ToList();

        return list;
    }

    internal virtual SyntaxToken VisitListSeparator(SyntaxToken separator) {
        return VisitToken(separator);
    }

    internal virtual SyntaxTokenList VisitList(SyntaxTokenList list) {
        if (list is null)
            return null;

        SyntaxTokenListBuilder alternate = null;
        var count = list.Count;
        var index = -1;

        foreach (var item in list) {
            index++;
            var visited = VisitToken(item);

            if (item != visited && alternate is null) {
                alternate = new SyntaxTokenListBuilder(count);
                alternate.Add(list, 0, index);
            }

            if (alternate is not null && visited.kind != SyntaxKind.None)
                alternate.Add(visited);
        }

        if (alternate is not null)
            return alternate.ToList();

        return list;
    }

    internal virtual SyntaxTriviaList VisitList(SyntaxTriviaList list) {
        var count = list.Count;

        if (count != 0) {
            SyntaxTriviaListBuilder alternate = null;
            var index = -1;

            foreach (var item in list) {
                index++;
                var visited = VisitListElement(item);

                if (visited != item && alternate is null) {
                    alternate = new SyntaxTriviaListBuilder(count);
                    alternate.Add(list, 0, index);
                }

                if (alternate != null && visited.kind != SyntaxKind.None)
                    alternate.Add(visited);
            }

            if (alternate is not null)
                return alternate.ToList();
        }

        return list;
    }

    internal virtual SyntaxTrivia VisitListElement(SyntaxTrivia element) {
        return VisitTrivia(element);
    }
}
