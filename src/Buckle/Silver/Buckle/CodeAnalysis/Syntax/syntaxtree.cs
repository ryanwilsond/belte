using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {

    internal class SyntaxTree {
        public Expression root { get; }
        public Token eof { get; }
        public DiagnosticQueue diagnostics;

        public SyntaxTree(Expression root_, Token eof_, DiagnosticQueue diagnostics_) {
            root = root_;
            eof = eof_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
        }

        public static SyntaxTree Parse(string line) {
            Parser parser = new Parser(line);
            return parser.Parse();
        }

        public static IEnumerable<Token> ParseTokens(string text) {
            Lexer lexer = new Lexer(text);
            while (true) {
                var token = lexer.LexNext();
                if (token.type == SyntaxType.EOF) break;
                yield return token;
            }
        }
    }
}
