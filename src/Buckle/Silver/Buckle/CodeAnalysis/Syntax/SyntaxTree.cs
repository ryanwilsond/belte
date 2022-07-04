using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxTree {
    internal CompilationUnit root { get; }
    internal Token endOfFile { get; }
    internal SourceText text { get; }
    internal BelteDiagnosticQueue diagnostics;

    private delegate void ParseHandler(
        SyntaxTree syntaxTree, out CompilationUnit root, out BelteDiagnosticQueue diagnostics);

    private SyntaxTree(SourceText text_, ParseHandler handler) {
        text = text_;
        diagnostics = new BelteDiagnosticQueue();

        handler(this, out var root_, out diagnostics);

        root = root_;
    }

    internal static SyntaxTree Load(string fileName, string text) {
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    internal static SyntaxTree Load(string fileName) {
        var text = File.ReadAllText(fileName);
        var sourceText = SourceText.From(text, fileName);
        return Parse(sourceText);
    }

    internal static SyntaxTree Parse(string text) {
        var sourceText = SourceText.From(text);
        return Parse(sourceText);
    }

    internal static SyntaxTree Parse(SourceText text) {
        return new SyntaxTree(text, Parse);
    }

    internal static ImmutableArray<Token> ParseTokens(string text, bool includeEOF = false) {
        var sourceText = SourceText.From(text);
        return ParseTokens(sourceText, includeEOF);
    }

    internal static ImmutableArray<Token> ParseTokens(
        string text, out BelteDiagnosticQueue diagnostics, bool includeEOF = false) {
        var sourceText = SourceText.From(text);
        return ParseTokens(sourceText, out diagnostics, includeEOF);
    }

    internal static ImmutableArray<Token> ParseTokens(SourceText text, bool includeEOF = false) {
        return ParseTokens(text, out _, includeEOF);
    }

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
