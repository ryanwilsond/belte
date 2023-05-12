
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal static class SyntaxFactory {
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
}
