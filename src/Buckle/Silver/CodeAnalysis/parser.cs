using System.Collections.Generic;

namespace Buckle.CodeAnalysis {

    class Parser {
        private readonly Token[] tokens_;
        private int pos_;
        public List<Diagnostic> diagnostics;

        public Parser(string text) {
            diagnostics = new List<Diagnostic>();
            var tokens = new List<Token>();
            Lexer lexer = new Lexer(text);
            Token token;

            do {
                token = lexer.Next();

                if (token.type != SyntaxType.WHITESPACE && token.type != SyntaxType.Invalid)
                    tokens.Add(token);
            } while (token.type != SyntaxType.EOF);

            tokens_ = tokens.ToArray();
            diagnostics.AddRange(lexer.diagnostics);
        }

        public SyntaxTree Parse() {
            var expr = ParseTerm();
            var eof = Match(SyntaxType.EOF);
            return new SyntaxTree(expr, eof, diagnostics);
        }

        public Expression ParseTerm() {
            var left = ParseFactor();

            while (current.type == SyntaxType.PLUS || current.type == SyntaxType.MINUS) {
                var op = Next();
                var right = ParseFactor();
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        public Expression ParseFactor() {
            var left = ParsePrimaryExpression();

            while (current.type == SyntaxType.ASTERISK || current.type == SyntaxType.SOLIDUS) {
                var op = Next();
                var right = ParsePrimaryExpression();
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        private Expression ParsePrimaryExpression() {
            if (current.type == SyntaxType.LPAREN) {
                var left = Next();
                var expr = ParseTerm();
                var right = Match(SyntaxType.RPAREN);
                return new ParenExpression(left, expr, right);
            }

            var token = Match(SyntaxType.NUMBER);
            return new NumberNode(token);
        }

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Add(new Diagnostic(DiagnosticType.error, $"expected token of type '{type}', got '{current.type}'"));
            return new Token(type, current.pos, null, null);
        }

        private Token Next() {
            Token cur = current;
            pos_++;
            return cur;
        }

        private Token Peek(int offset) {
            int index = pos_ + offset;
            if (index >= tokens_.Length) return tokens_[tokens_.Length-1];
            return tokens_[index];
        }

        private Token current => Peek(0);
    }

}
