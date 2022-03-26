using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal class SyntaxTree {
        public Expression root { get; }
        public Token eof { get; }
        public SourceText text { get; }
        public DiagnosticQueue diagnostics;

        public SyntaxTree(SourceText text_, Expression root_, Token eof_, DiagnosticQueue diagnostics_) {
            root = root_;
            eof = eof_;
            text = text_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
        }

        public static SyntaxTree Parse(string text) {
            var sourceText = SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text) {
            Parser parser = new Parser(text);
            return parser.Parse();
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
