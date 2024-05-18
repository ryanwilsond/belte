using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// A factory for various GreenNodes.
/// </summary>
internal static partial class SyntaxFactory {
    /// <summary>
    /// Creates a <see cref="SyntaxToken" />.
    /// </summary>
    internal static SyntaxToken Token(SyntaxKind kind) {
        return new SyntaxToken(kind, null, null);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with text.
    /// </summary>
    internal static SyntaxToken Token(SyntaxKind kind, string text) {
        return new SyntaxToken(kind, text, null);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with text and value.
    /// </summary>
    internal static SyntaxToken Token(SyntaxKind kind, string text, object value) {
        return new SyntaxToken(kind, text, value);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxKind"/> with trivia.
    /// </summary>
    internal static SyntaxToken Token(GreenNode leadingTrivia, SyntaxKind kind, GreenNode trailingTrivia) {
        return new SyntaxToken(kind, SyntaxFacts.GetText(kind), null, leadingTrivia, trailingTrivia);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with a predefined full width, text, a value, and trivia.
    /// </summary>
    internal static SyntaxToken Token(
        SyntaxKind kind, int fullWidth, string text, object value, GreenNode leading, GreenNode trailing) {
        return new SyntaxToken(kind, fullWidth, text, value, leading, trailing);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxToken" /> with a text, a value, trivia, and diagnostics.
    /// </summary>
    internal static SyntaxToken Token(
        SyntaxKind kind, string text, object value,
        GreenNode leading, GreenNode trailing, Diagnostic[] diagnostics) {
        return new SyntaxToken(kind, text, value, leading, trailing, diagnostics);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxTrivia" /> with text and diagnostics.
    /// </summary>
    internal static SyntaxTrivia Trivia(SyntaxKind kind, string text, Diagnostic[] diagnostics) {
        return new SyntaxTrivia(kind, text, diagnostics);
    }

    /// <summary>
    /// Creates a missing <see cref="SyntaxToken" />.
    /// </summary>
    internal static SyntaxToken Missing(SyntaxKind kind) {
        return SyntaxToken.CreateMissing(kind, null, null);
    }

    /// <summary>
    /// Creates a <see cref="LiteralExpressionSyntax" />.
    /// </summary>
    internal static LiteralExpressionSyntax Literal(SyntaxToken token) => LiteralExpression(token);

    /// <summary>
    /// Creates a <see cref="LiteralExpressionSyntax" /> with a value.
    /// </summary>
    internal static LiteralExpressionSyntax Literal(SyntaxToken token, object value) {
        return LiteralExpression(
            Token(token.kind, token.fullWidth, token.text, value, token.GetLeadingTrivia(), token.GetTrailingTrivia())
        );
    }

    /// <summary>
    /// Creates a <see cref="EmptyExpressionSyntax" />.
    /// </summary>
    internal static EmptyExpressionSyntax Empty() => EmptyExpression();
}
