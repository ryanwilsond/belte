using Buckle.CodeAnalysis.Syntax.InternalSyntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

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
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(string text, bool includeEOF = false) {
        var sourceText = SourceText.From(text);

        return ParseTokens(sourceText, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="diagnostics">Diagnostics produced from parsing.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(
        string text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var sourceText = SourceText.From(text);

        return ParseTokens(sourceText, out diagnostics, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(SourceText text, bool includeEOF = false) {
        return ParseTokens(text, out _, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="diagnostics">Diagnostics produced from parsing.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static InternalSyntax.SyntaxList<InternalSyntax.SyntaxToken> ParseTokens(
        SourceText text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var tokens = new SyntaxListBuilder<InternalSyntax.SyntaxToken>(32);

        void ParseTokens(SyntaxTree syntaxTree, out CompilationUnitSyntax root) {
            root = null;
            var lexer = new InternalSyntax.Lexer(syntaxTree);

            while (true) {
                var token = lexer.LexNext();

                if (token.kind == SyntaxKind.EndOfFileToken)
                    root = (CompilationUnitSyntax)InternalSyntax.SyntaxFactory.CompilationUnit(
                        InternalSyntax.SyntaxFactory.List<InternalSyntax.MemberSyntax>(),
                        token
                    ).CreateRed();

                if (token.kind != SyntaxKind.EndOfFileToken || includeEOF)
                    tokens.Add(token);

                if (token.kind == SyntaxKind.EndOfFileToken)
                    break;
            }
        }

        var syntaxTree = SyntaxTree.Create(text, ParseTokens);
        diagnostics = syntaxTree.GetDiagnostics();

        return tokens.ToList();
    }
}