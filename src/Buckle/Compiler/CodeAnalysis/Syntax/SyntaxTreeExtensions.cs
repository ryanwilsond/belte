using Buckle.CodeAnalysis.Syntax.InternalSyntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All methods related to performing only the lexing phase. Used for testing.
/// </summary>
public static class SyntaxTreeExtensions {
    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(
        string text,
        bool includeEOF = false) {
        var sourceText = SourceText.From(text);

        return ParseTokens(sourceText, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(
        SourceText text,
        bool includeEOF = false) {
        var tokens = new InternalSyntax.SyntaxListBuilder<InternalSyntax.SyntaxToken>(32);
        var lexer = new Lexer(text, true);

        while (true) {
            var token = lexer.LexNext(LexerMode.Syntax);

            if (token.kind != SyntaxKind.EndOfFileToken || includeEOF)
                tokens.Add(token);

            if (token.kind == SyntaxKind.EndOfFileToken)
                break;
        }

        return tokens.ToList();
    }
}
