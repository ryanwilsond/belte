using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Authoring;

/// <summary>
/// Classifies parsed SyntaxNodes.
/// </summary>
public static class Classifier {
    /// <summary>
    /// Classifies SyntaxNodes in a <see cref="SyntaxTree" /> within a <see cref="TextSpan" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to classify.</param>
    /// <param name="span">What segment of the <see cref="SyntaxTree" /> to classify.</param>
    /// <returns>All Classifications made within the <see cref="TextSpan" /> of the <see cref="SyntaxTree" />.</returns>
    public static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree syntaxTree, TextSpan span) {
        var result = ImmutableArray.CreateBuilder<ClassifiedSpan>();
        ClassifyNode(syntaxTree.GetRoot(), span, result);

        return result.ToImmutable();
    }

    private static void ClassifyNode(
        SyntaxNodeOrToken node, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName = false) {
        if (node is null)
            return;

        if (node.fullSpan != null && !node.fullSpan.OverlapsWith(span))
            return;

        if (node.isToken) {
            ClassifyToken(node.AsToken(), span, result, isTypeName);
        } else if (node.AsNode() is TypeSyntax) {
            var inAttribute = false;
            isTypeName = false;

            foreach (var child in node.ChildNodesAndTokens()) {
                // Does not matter that it catches on array brackets because they do not contain identifiers
                if (child.kind == SyntaxKind.OpenBracketToken)
                    inAttribute = true;
                if (child.kind == SyntaxKind.CloseBracketToken)
                    inAttribute = false;

                if (child.kind == SyntaxKind.IdentifierToken && !inAttribute)
                    isTypeName = true;

                ClassifyNode(child, span, result, isTypeName);
            }
        } else {
            foreach (var child in node.ChildNodesAndTokens())
                ClassifyNode(child, span, result);
        }
    }

    private static void ClassifyToken(
        SyntaxToken token, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName) {
        foreach (var trivia in token.leadingTrivia)
            ClassifyTrivia(trivia, span, result);

        AddClassification(token.kind, token.span, span, result, isTypeName);

        foreach (var trivia in token.trailingTrivia)
            ClassifyTrivia(trivia, span, result);
    }

    private static void ClassifyTrivia(
        SyntaxTrivia trivia, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
        AddClassification(trivia.kind, trivia.span, span, result, false);
    }

    private static void AddClassification(SyntaxKind elementKind, TextSpan elementSpan,
        TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName) {
        if (!elementSpan.OverlapsWith(span))
            return;

        var classification = GetClassification(elementKind, isTypeName);
        var adjustedStart = Math.Max(elementSpan.start, span.start);
        var adjustedEnd = Math.Min(elementSpan.end, span.end);
        var adjustedSpan = TextSpan.FromBounds(adjustedStart, adjustedEnd);

        var classifiedSpan = new ClassifiedSpan(adjustedSpan, classification);
        result.Add(classifiedSpan);
    }

    private static Classification GetClassification(SyntaxKind kind, bool isTypeName) {
        var isKeyword = kind.IsKeyword();
        var isNumber = kind == SyntaxKind.NumericLiteralToken;
        var isIdentifier = kind == SyntaxKind.IdentifierToken;
        var isString = kind == SyntaxKind.StringLiteralToken;
        var isComment = kind.IsComment();

        if (isTypeName)
            return Classification.Type;
        else if (isKeyword)
            return Classification.Keyword;
        else if (isIdentifier)
            return Classification.Identifier;
        else if (isNumber)
            return Classification.Number;
        else if (isString)
            return Classification.String;
        else if (isComment)
            return Classification.Comment;
        else
            return Classification.Text;
    }
}
