using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Authoring;

/// <summary>
/// Simplified classification of token.
/// </summary>
internal enum Classification {
    Identifier,
    Keyword,
    TypeName,
    Number,
    String,
    Comment,
    Text,
}

/// <summary>
/// Span of where a classification refers to.
/// </summary>
internal sealed class ClassifiedSpan {
    /// <summary>
    /// Creates a classified span.
    /// </summary>
    /// <param name="span">Span of where referring to</param>
    /// <param name="classification">Classification</param>
    internal ClassifiedSpan(TextSpan span, Classification classification) {
        this.span = span;
        this.classification = classification;
    }

    /// <summary>
    /// Span of where referring to.
    /// </summary>
    internal TextSpan span { get; }

    /// <summary>
    /// Classification.
    /// </summary>
    internal Classification classification { get; }
}

/// <summary>
/// Classifies parsed nodes.
/// </summary>
internal static class Classifier {
    /// <summary>
    /// Classifies nodes in a syntax tree within a span.
    /// </summary>
    /// <param name="syntaxTree">Tree to classify</param>
    /// <param name="span">What segment of the tree to classify</param>
    /// <returns>All classifications made within the span of the tree</returns>
    internal static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree syntaxTree, TextSpan span) {
        var result = ImmutableArray.CreateBuilder<ClassifiedSpan>();
        ClassifyNode(syntaxTree.root, span, result);
        return result.ToImmutable();
    }

    private static void ClassifyNode(
        Node node, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName = false) {
        if (node == null)
            return;

        if (node.fullSpan != null && !node.fullSpan.OverlapsWith(span))
            return;

        if (node is Token token)
            ClassifyToken(token, span, result, isTypeName);

        if (node is TypeClause) {
            bool inAttribute = false;
            isTypeName = false;

            foreach (var child in node.GetChildren()) {
                // Does not matter that it catches on array brackets because they do not contain identifiers
                if (child.type == SyntaxType.OpenBracketToken)
                    inAttribute = true;
                if (child.type == SyntaxType.CloseBracketToken)
                    inAttribute = false;

                if (child.type == SyntaxType.IdentifierToken && !inAttribute)
                    isTypeName = true;

                ClassifyNode(child, span, result, isTypeName);
            }
        } else {
            foreach (var child in node.GetChildren())
                ClassifyNode(child, span, result);
        }
    }

    private static void ClassifyToken(
        Token token, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName) {
        foreach (var trivia in token.leadingTrivia)
            ClassifyTrivia(trivia, span, result);

        AddClassification(token.type, token.span, span, result, isTypeName);

        foreach (var trivia in token.trailingTrivia)
            ClassifyTrivia(trivia, span, result);
    }

    private static void ClassifyTrivia(
        SyntaxTrivia trivia, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
        AddClassification(trivia.type, trivia.span, span, result, false);
    }

    private static void AddClassification(SyntaxType elementType, TextSpan elementSpan,
        TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result, bool isTypeName) {
        if (!elementSpan.OverlapsWith(span))
            return;

        var classification = GetClassification(elementType, isTypeName);
        var adjustedStart = Math.Max(elementSpan.start, span.start);
        var adjustedEnd = Math.Min(elementSpan.end, span.end);
        var adjustedSpan = TextSpan.FromBounds(adjustedStart, adjustedEnd);

        var classifiedSpan = new ClassifiedSpan(adjustedSpan, classification);
        result.Add(classifiedSpan);
    }

    private static Classification GetClassification(SyntaxType type, bool isTypeName) {
        var isKeyword = type.IsKeyword();
        var isNumber = type == SyntaxType.NumericLiteralToken;
        var isIdentifier = type == SyntaxType.IdentifierToken;
        var isString = type == SyntaxType.StringLiteralToken;
        var isComment = type.IsComment();

        if (isTypeName)
            return Classification.TypeName;
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
