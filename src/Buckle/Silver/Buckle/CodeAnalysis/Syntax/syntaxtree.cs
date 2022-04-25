using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class SyntaxTree {
        public CompilationUnit root { get; }
        public Token endOfFile { get; }
        public SourceText text { get; }
        public DiagnosticQueue diagnostics;

        private delegate void ParseHandler(
            SyntaxTree syntaxTree, out CompilationUnit root, out DiagnosticQueue diagnostics);

        private SyntaxTree(SourceText text_, ParseHandler handler) {
            text = text_;
            diagnostics = new DiagnosticQueue();

            handler(this, out var root_, out diagnostics);

            root = root_;
        }

        public static SyntaxTree Load(string fileName, string text) {
            var sourceText = SourceText.From(text, fileName);
            return Parse(sourceText);
        }

        public static SyntaxTree Load(string fileName) {
            var text = File.ReadAllText(fileName);
            var sourceText = SourceText.From(text, fileName);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(string text) {
            var sourceText = SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text) {
            return new SyntaxTree(text, Parse);
        }

        public static ImmutableArray<Token> ParseTokens(string text, bool includeEOF = false) {
            var sourceText = SourceText.From(text);
            return ParseTokens(sourceText, includeEOF);
        }

        public static ImmutableArray<Token> ParseTokens(
            string text, out DiagnosticQueue diagnostics, bool includeEOF = false) {
            var sourceText = SourceText.From(text);
            return ParseTokens(sourceText, out diagnostics, includeEOF);
        }

        public static ImmutableArray<Token> ParseTokens(SourceText text, bool includeEOF = false) {
            return ParseTokens(text, out _, includeEOF);
        }

        public static ImmutableArray<Token> ParseTokens(
            SourceText text, out DiagnosticQueue diagnostics, bool includeEOF = false) {
            var tokens = new List<Token>();

            void ParseTokens(SyntaxTree syntaxTree, out CompilationUnit root, out DiagnosticQueue diagnostics) {
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

                diagnostics = new DiagnosticQueue();
                diagnostics.Move(lexer.diagnostics);
            }

            var syntaxTree = new SyntaxTree(text, ParseTokens);
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(syntaxTree.diagnostics);
            return tokens.ToImmutableArray();
        }

        private static void Parse(SyntaxTree syntaxTree, out CompilationUnit root, out DiagnosticQueue diagnostics) {
            var parser = new Parser(syntaxTree);
            root = parser.ParseCompilationUnit();
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(parser.diagnostics);
        }
    }
}
