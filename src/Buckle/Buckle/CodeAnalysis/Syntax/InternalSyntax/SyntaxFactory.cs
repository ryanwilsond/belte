using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal static partial class SyntaxFactory {
    internal static SyntaxTrivia Skipped(string text) {
        return new SyntaxTrivia(SyntaxKind.SkippedTokenTrivia, text);
    }

    internal static SyntaxTrivia Skipped(SyntaxToken token) {
        return new SyntaxTrivia(SyntaxKind.SkippedTokenTrivia, token.text);
    }

    internal static SyntaxToken Token(
        SyntaxKind kind, int fullWidth, string text, object value, GreenNode leading, GreenNode trailing) {
        return new SyntaxToken(kind, fullWidth, text, value, leading, trailing);
    }

    internal static SyntaxToken Token(
        SyntaxKind kind, int fullWidth, string text, object value,
        GreenNode leading, GreenNode trailing, Diagnostic[] diagnostics) {
        return new SyntaxToken(kind, fullWidth, text, value, leading, trailing, diagnostics);
    }

    internal static SyntaxToken Token(
        SyntaxKind kind, string text, object value,
        GreenNode leading, GreenNode trailing, Diagnostic[] diagnostics) {
        return new SyntaxToken(kind, text, value, leading, trailing, diagnostics);
    }

    internal static SyntaxTrivia Trivia(SyntaxKind kind, string text) {
        return new SyntaxTrivia(kind, text);
    }

    internal static SyntaxToken Token(SyntaxKind kind) {
        return new SyntaxToken(kind, null, null);
    }

    internal static SyntaxToken Missing(SyntaxKind kind) {
        return SyntaxToken.CreateMissing(kind, null, null);
    }

    internal static SyntaxToken Token(SyntaxKind kind, string text) {
        return new SyntaxToken(kind, text, null);
    }

    internal static EmptyExpressionSyntax Empty() {
        return new EmptyExpressionSyntax(null);
    }

    internal static LiteralExpressionSyntax Literal(SyntaxToken token) {
        return new LiteralExpressionSyntax(token);
    }

    internal static LiteralExpressionSyntax Literal(SyntaxToken token, object value) {
        return new LiteralExpressionSyntax(
            Token(token.kind, token.fullWidth, token.text, value, token.GetLeadingTrivia(), token.GetTrailingTrivia())
        );
    }

    internal static SyntaxList<T> List<T>() where T : BelteSyntaxNode {
        return new SyntaxList<T>(null);
    }
}
