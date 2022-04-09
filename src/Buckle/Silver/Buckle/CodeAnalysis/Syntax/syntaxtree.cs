using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

        public static SyntaxTree Parse(string text) {
            var sourceText = SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text) {
            return new SyntaxTree(text, Parse);
        }

        public static ImmutableArray<Token> ParseTokens(string text) {
            var sourceText = SourceText.From(text);
            return ParseTokens(sourceText);
        }

        public static ImmutableArray<Token> ParseTokens(string text, out DiagnosticQueue diagnostics) {
            var sourceText = SourceText.From(text);
            return ParseTokens(sourceText, out diagnostics);
        }

        public static ImmutableArray<Token> ParseTokens(SourceText text) {
            return ParseTokens(text, out _);
        }

        public static ImmutableArray<Token> ParseTokens(SourceText text, out DiagnosticQueue diagnostics) {
            var tokens = new List<Token>();

            void ParseTokens(SyntaxTree syntaxTree, out CompilationUnit root, out DiagnosticQueue diagnostics) {
                root = null;
                Lexer lexer = new Lexer(syntaxTree);

                while (true) {
                    var token = lexer.LexNext();

                    if (token.type == SyntaxType.EOF) {
                        root = new CompilationUnit(syntaxTree, ImmutableArray<Member>.Empty, token);
                        break;
                    }

                    tokens.Add(token);
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
