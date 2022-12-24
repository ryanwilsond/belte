using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using InternalSyntax = Buckle.CodeAnalysis.Syntax.InternalSyntax;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Tree of SyntaxNodes produced from the <see cref="Parser" />.
/// </summary>
internal sealed class SyntaxTree {
    private SyntaxTree(SourceText text, ParseHandler handler) {
        this.text = text;
        diagnostics = new BelteDiagnosticQueue();

        handler(this, out var _root, out diagnostics);

        root = _root;
    }

    private delegate void ParseHandler(
        SyntaxTree syntaxTree, out CompilationUnitSyntax root, out BelteDiagnosticQueue diagnostics);

    /// <summary>
    /// Root <see cref="SyntaxNode" />, does not represent something in a source file rather the entire source file.
    /// </summary>
    internal CompilationUnitSyntax root { get; }

    /// <summary>
    /// EOF <see cref="SyntaxToken" />.
    /// </summary>
    internal SyntaxToken endOfFile { get; }

    /// <summary>
    /// <see cref="SourceText" /> the <see cref="SyntaxTree" /> was created from.
    /// </summary>
    internal SourceText text { get; }

    /// <summary>
    /// Diagnostics relating to <see cref="SyntaxTree" />.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics;

    /// <summary>
    /// Create a <see cref="SyntaxTree" /> from a source file.
    /// </summary>
    /// <param name="fileName">File name of source file.</param>
    /// <param name="text">Content of source file.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Load(string fileName, string text) {
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxTree" /> from a source file, uses file name to open and read the file directly.
    /// </summary>
    /// <param name="fileName">File name of source file.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Load(string fileName) {
        var text = File.ReadAllText(fileName);
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    /// <summary>
    /// Parses text (not necessarily related to a source file).
    /// </summary>
    /// <param name="text">Text to generate <see cref="SyntaxTree" /> from.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Parse(string text) {
        var sourceText = SourceText.From(text);
        return Parse(sourceText);
    }

    /// <summary>
    /// Parses <see cref="SourceText" />.
    /// </summary>
    /// <param name="text">Text to generate <see cref="SyntaxTree" /> from.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Parse(SourceText text) {
        return new SyntaxTree(text, Parse);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static ImmutableArray<SyntaxToken> ParseTokens(string text, bool includeEOF = false) {
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
    internal static ImmutableArray<SyntaxToken> ParseTokens(
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
    internal static ImmutableArray<SyntaxToken> ParseTokens(SourceText text, bool includeEOF = false) {
        return ParseTokens(text, out _, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of SyntaxTokens (not a <see cref="SyntaxTree" />).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="diagnostics">Diagnostics produced from parsing.</param>
    /// <param name="includeEOF">If to include the EOF <see cref="SyntaxToken" /> at the end.</param>
    /// <returns>SyntaxTokens in order.</returns>
    internal static ImmutableArray<SyntaxToken> ParseTokens(
        SourceText text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var tokens = new List<SyntaxToken>();

        void ParseTokens(SyntaxTree syntaxTree, out CompilationUnitSyntax root, out BelteDiagnosticQueue diagnostics) {
            root = null;
            InternalSyntax.Lexer lexer = new InternalSyntax.Lexer(syntaxTree);

            while (true) {
                var token = lexer.LexNext();

                if (token.kind == SyntaxKind.EndOfFileToken)
                    root = new CompilationUnitSyntax(syntaxTree, ImmutableArray<MemberSyntax>.Empty, token);

                if (token.kind != SyntaxKind.EndOfFileToken || includeEOF)
                    tokens.Add(token);

                if (token.kind == SyntaxKind.EndOfFileToken)
                    break;
            }

            diagnostics = new BelteDiagnosticQueue();
            diagnostics.Move(lexer.diagnostics);
        }

        var syntaxTree = new SyntaxTree(text, ParseTokens);
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(syntaxTree.diagnostics);
        return tokens.ToImmutableArray();
    }

    private static void Parse(
        SyntaxTree syntaxTree, out CompilationUnitSyntax root, out BelteDiagnosticQueue diagnostics) {
        var parser = new InternalSyntax.Parser(syntaxTree);
        root = parser.ParseCompilationUnit();
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(parser.diagnostics);
    }
}
