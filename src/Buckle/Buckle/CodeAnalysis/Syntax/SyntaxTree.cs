using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Tree of nodes produced from the parser.
/// </summary>
internal sealed class SyntaxTree {
    private SyntaxTree(SourceText text, ParseHandler handler) {
        this.text = text;
        diagnostics = new BelteDiagnosticQueue();

        handler(this, out var root_, out diagnostics);

        root = root_;
    }

    private delegate void ParseHandler(
        SyntaxTree syntaxTree, out CompilationUnit root, out BelteDiagnosticQueue diagnostics);

    /// <summary>
    /// Root node, does not represent something in a source file rather the entire source file.
    /// </summary>
    internal CompilationUnit root { get; }

    /// <summary>
    /// EOF token.
    /// </summary>
    internal Token endOfFile { get; }

    /// <summary>
    /// Source text tree was created from.
    /// </summary>
    internal SourceText text { get; }

    /// <summary>
    /// Diagnostics relating to syntax tree.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics;

    /// <summary>
    /// Create a syntax tree from a source file.
    /// </summary>
    /// <param name="fileName">File name of source file</param>
    /// <param name="text">Content of source file</param>
    /// <returns>Parsed result as syntax tree</returns>
    internal static SyntaxTree Load(string fileName, string text) {
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    /// <summary>
    /// Creates a syntax tree from a source file, uses file name to open and read the file directly.
    /// </summary>
    /// <param name="fileName">File name of source file</param>
    /// <returns>Parsed result as syntax tree</returns>
    internal static SyntaxTree Load(string fileName) {
        var text = File.ReadAllText(fileName);
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    /// <summary>
    /// Parses text (not necessarily related to a source file).
    /// </summary>
    /// <param name="text">Text to generate syntax tree from</param>
    /// <returns>Parsed result as syntax tree</returns>
    internal static SyntaxTree Parse(string text) {
        var sourceText = SourceText.From(text);
        return Parse(sourceText);
    }

    /// <summary>
    /// Parses source text.
    /// </summary>
    /// <param name="text">Text to generate syntax tree from</param>
    /// <returns>Parsed result as syntax tree</returns>
    internal static SyntaxTree Parse(SourceText text) {
        return new SyntaxTree(text, Parse);
    }

    /// <summary>
    /// Parses text into an array of tokens (not a syntax tree).
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <param name="includeEOF">If to include the EOF token at the end</param>
    /// <returns>Tokens in order</returns>
    internal static ImmutableArray<Token> ParseTokens(string text, bool includeEOF = false) {
        var sourceText = SourceText.From(text);
        return ParseTokens(sourceText, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of tokens (not a syntax tree).
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <param name="diagnostics">Diagnostics produced from parsing</param>
    /// <param name="includeEOF">If to include the EOF token at the end</param>
    /// <returns>Tokens in order</returns>
    internal static ImmutableArray<Token> ParseTokens(
        string text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var sourceText = SourceText.From(text);
        return ParseTokens(sourceText, out diagnostics, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of tokens (not a syntax tree).
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <param name="includeEOF">If to include the EOF token at the end</param>
    /// <returns>Tokens in order</returns>
    internal static ImmutableArray<Token> ParseTokens(SourceText text, bool includeEOF = false) {
        return ParseTokens(text, out _, includeEOF);
    }

    /// <summary>
    /// Parses text into an array of tokens (not a syntax tree).
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <param name="diagnostics">Diagnostics produced from parsing</param>
    /// <param name="includeEOF">If to include the EOF token at the end</param>
    /// <returns>Tokens in order</returns>
    internal static ImmutableArray<Token> ParseTokens(
        SourceText text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var tokens = new List<Token>();

        void ParseTokens(SyntaxTree syntaxTree, out CompilationUnit root, out BelteDiagnosticQueue diagnostics) {
            root = null;
            Lexer lexer = new Lexer(syntaxTree);

            while (true) {
                var token = lexer.LexNext();

                if (token.type == SyntaxType.END_OF_FILE_TOKEN)
                    root = new CompilationUnit(syntaxTree, ImmutableArray<Member>.Empty, token);

                if (token.type != SyntaxType.END_OF_FILE_TOKEN || includeEOF)
                    tokens.Add(token);

                if (token.type == SyntaxType.END_OF_FILE_TOKEN)
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

    private static void Parse(SyntaxTree syntaxTree, out CompilationUnit root, out BelteDiagnosticQueue diagnostics) {
        var parser = new Parser(syntaxTree);
        root = parser.ParseCompilationUnit();
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(parser.diagnostics);
    }
}
