using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class SyntaxTree {
        public CompilationUnit root { get; }
        public Token eof { get; }
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

        public static IEnumerable<Token> ParseTokens(string text) {
            var sourceText = SourceText.From(text);
            return ParseTokens(sourceText);
        }

        public static IEnumerable<Token> ParseTokens(SourceText text) {
            Lexer lexer = new Lexer(text);
            while (true) {
                var token = lexer.LexNext();
                if (token.type == SyntaxType.EOF) break;
                yield return token;
            }
        }
    }
}
