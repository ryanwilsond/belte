using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    internal static SyntaxNode Replace<TNode>(
        SyntaxNode root,
        IEnumerable<TNode> nodes = null,
        Func<TNode, TNode, SyntaxNode> computeReplacementNode = null,
        IEnumerable<SyntaxToken> tokens = null,
        Func<SyntaxToken, SyntaxToken, SyntaxToken> computeReplacementToken = null,
        IEnumerable<SyntaxTrivia> trivia = null,
        Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> computeReplacementTrivia = null)
        where TNode : SyntaxNode {
        var replacer = new Replacer<TNode>(
            nodes, computeReplacementNode,
            tokens, computeReplacementToken,
            trivia, computeReplacementTrivia
        );

        if (replacer.hasWork)
            return replacer.Visit(root);
        else
            return root;
    }

    internal static SyntaxToken Replace(
        SyntaxToken root,
        IEnumerable<SyntaxNode> nodes = null,
        Func<SyntaxNode, SyntaxNode, SyntaxNode> computeReplacementNode = null,
        IEnumerable<SyntaxToken> tokens = null,
        Func<SyntaxToken, SyntaxToken, SyntaxToken> computeReplacementToken = null,
        IEnumerable<SyntaxTrivia> trivia = null,
        Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> computeReplacementTrivia = null) {
        var replacer = new Replacer<SyntaxNode>(
            nodes, computeReplacementNode,
            tokens, computeReplacementToken,
            trivia, computeReplacementTrivia
        );

        if (replacer.hasWork)
            return replacer.VisitToken(root);
        else
            return root;
    }

    internal static SyntaxNode ReplaceNodeInList(SyntaxNode root, SyntaxNode originalNode, IEnumerable<SyntaxNode> newNodes) {
        return new NodeListEditor(originalNode, newNodes, ListEditKind.Replace).Visit(root);
    }

    internal static SyntaxNode InsertNodeInList(SyntaxNode root, SyntaxNode nodeInList, IEnumerable<SyntaxNode> nodesToInsert, bool insertBefore) {
        return new NodeListEditor(nodeInList, nodesToInsert, insertBefore ? ListEditKind.InsertBefore : ListEditKind.InsertAfter).Visit(root);
    }

    internal static SyntaxNode ReplaceTokenInList(SyntaxNode root, SyntaxToken tokenInList, IEnumerable<SyntaxToken> newTokens) {
        return new TokenListEditor(tokenInList, newTokens, ListEditKind.Replace).Visit(root);
    }

    internal static SyntaxNode InsertTokenInList(SyntaxNode root, SyntaxToken tokenInList, IEnumerable<SyntaxToken> newTokens, bool insertBefore) {
        return new TokenListEditor(tokenInList, newTokens, insertBefore ? ListEditKind.InsertBefore : ListEditKind.InsertAfter).Visit(root);
    }

    internal static SyntaxNode ReplaceTriviaInList(SyntaxNode root, SyntaxTrivia triviaInList, IEnumerable<SyntaxTrivia> newTrivia) {
        return new TriviaListEditor(triviaInList, newTrivia, ListEditKind.Replace).Visit(root);
    }

    internal static SyntaxNode InsertTriviaInList(SyntaxNode root, SyntaxTrivia triviaInList, IEnumerable<SyntaxTrivia> newTrivia, bool insertBefore) {
        return new TriviaListEditor(triviaInList, newTrivia, insertBefore ? ListEditKind.InsertBefore : ListEditKind.InsertAfter).Visit(root);
    }

    internal static SyntaxToken ReplaceTriviaInList(SyntaxToken root, SyntaxTrivia triviaInList, IEnumerable<SyntaxTrivia> newTrivia) {
        return new TriviaListEditor(triviaInList, newTrivia, ListEditKind.Replace).VisitToken(root);
    }

    internal static SyntaxToken InsertTriviaInList(SyntaxToken root, SyntaxTrivia triviaInList, IEnumerable<SyntaxTrivia> newTrivia, bool insertBefore) {
        return new TriviaListEditor(triviaInList, newTrivia, insertBefore ? ListEditKind.InsertBefore : ListEditKind.InsertAfter).VisitToken(root);
    }

    private static InvalidOperationException GetItemNotListElementException() {
        return new InvalidOperationException("MissingListItem");
    }

    private static InvalidOperationException GetTokenNotListElementException() {
        return new InvalidOperationException("MissingTokenListItem");
    }
}
