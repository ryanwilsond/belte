using System.Collections.Immutable;
using System;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Authoring {

    internal enum Classification {
        Identifier,
        Keyword,
        Type,
        Number,
        String,
        Comment,
        Text,
    }

    internal sealed class ClassifiedSpan {
        public TextSpan span { get; }
        public Classification classification { get; }

        public ClassifiedSpan(TextSpan span_, Classification classification_) {
            span = span_;
            classification = classification_;
        }
    }

    internal static class Classifier {
        public static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree syntaxTree, TextSpan span) {
            var result = ImmutableArray.CreateBuilder<ClassifiedSpan>();
            ClassifyNode(syntaxTree.root, span, result);
            return result.ToImmutable();
        }

        private static void ClassifyNode(Node node, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            // TODO: put null check in GetChildren(), which should never return null
            if (node == null || node.fullSpan == null || !node.fullSpan.OverlapsWith(span))
                return;

            if (node is Token token)
                ClassifyToken(token, span, result);

            foreach (var child in node.GetChildren())
                ClassifyNode(child, span, result);
        }

        private static void ClassifyToken(Token token, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            foreach (var trivia in token.leadingTrivia)
                ClassifyTrivia(trivia, span, result);

            AddClassification(token.type, token.span, span, result);

            foreach (var trivia in token.trailingTrivia)
                ClassifyTrivia(trivia, span, result);
        }

        private static void ClassifyTrivia(
            SyntaxTrivia trivia, TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            AddClassification(trivia.type, trivia.span, span, result);
        }

        private static void AddClassification(SyntaxType elementType, TextSpan elementSpan,
            TextSpan span, ImmutableArray<ClassifiedSpan>.Builder result) {
            if (!elementSpan.OverlapsWith(span))
                return;

            var classification = GetClassification(elementType);
            var adjustedStart = Math.Max(elementSpan.start, span.start);
            var adjustedEnd = Math.Min(elementSpan.end, span.end);
            var adjustedSpan = TextSpan.FromBounds(adjustedStart, adjustedEnd);

            var classifiedSpan = new ClassifiedSpan(adjustedSpan, classification);
            result.Add(classifiedSpan);
        }

        private static Classification GetClassification(SyntaxType type) {
            var isKeyword = type.IsKeyword();
            var isNumber = type == SyntaxType.NUMBERIC_LITERAL_TOKEN;
            var isIdentifier = type == SyntaxType.IDENTIFIER_TOKEN;
            var isString = type == SyntaxType.STRING_LITERAL_TOKEN;
            var isComment = type.IsComment();

            if (isKeyword)
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
}
