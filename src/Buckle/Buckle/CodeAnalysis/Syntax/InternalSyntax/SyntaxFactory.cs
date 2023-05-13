
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal static partial class SyntaxFactory {
    internal static SyntaxTrivia Skipped(string text) {
        return new SyntaxTrivia(SyntaxKind.SkippedTokenTrivia, text);
    }

    internal static SyntaxToken Token(
        SyntaxKind kind, int fullWidth, string text, object value, GreenNode leading, GreenNode trailing) {
        return new SyntaxToken(kind, fullWidth, text, value, leading, trailing);
    }

    internal static SyntaxTrivia Trivia(SyntaxKind kind, string text) {
        return new SyntaxTrivia(kind, text);
    }

    internal static SyntaxToken Token(SyntaxKind kind) {
        return new SyntaxToken(kind, null, null);
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
}
