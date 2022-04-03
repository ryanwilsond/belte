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

        private SyntaxTree(SourceText text_) {
            Parser parser = new Parser(text_);
            var root_ = parser.ParseCompilationUnit();
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(parser.diagnostics);

            text = text_;
            root = root_;
        }

        public static SyntaxTree Parse(string text) {
            var sourceText = SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text) {
            return new SyntaxTree(text);
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
            IEnumerable<Token> LexTokens(Lexer lexer) {
                while (true) {
                    var token = lexer.LexNext();
                    if (token.type == SyntaxType.EOF) break;
                    yield return token;
                }
            }
            Lexer lexer = new Lexer(text);
            var result = LexTokens(lexer).ToImmutableArray();
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(lexer.diagnostics);
            return result;
        }
    }
}
