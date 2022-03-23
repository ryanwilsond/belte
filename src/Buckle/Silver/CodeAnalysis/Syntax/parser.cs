using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {

    internal class Parser {
        private readonly Token[] tokens_;
        private int pos_;
        public List<Diagnostic> diagnostics;

        public Parser(string text) {
            diagnostics = new List<Diagnostic>();
            var tokens = new List<Token>();
            Lexer lexer = new Lexer(text);
            Token token;

            do {
                token = lexer.LexNext();

                if (token.type != SyntaxType.WHITESPACE && token.type != SyntaxType.Invalid)
                    tokens.Add(token);
            } while (token.type != SyntaxType.EOF);

            tokens_ = tokens.ToArray();
            diagnostics.AddRange(lexer.diagnostics);
        }

        public SyntaxTree Parse() {
            var expr = ParseExpression();
            var eof = Match(SyntaxType.EOF);
            return new SyntaxTree(expr, eof, diagnostics);
        }

        private Expression ParseExpression(int parentPrecedence = 0) {
            Expression left;
            var unaryPrecedence = current.type.GetUnaryPrecedence();

            if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
                var op = Next();
                var operand = ParseExpression(unaryPrecedence);
                left = new UnaryExpression(op, operand);
            } else left = ParsePrimaryExpression();

            while (true) {
                int precedence = current.type.GetBinaryPrecedence();
                if (precedence == 0 || precedence <= parentPrecedence) break;
                var op = Next();
                var right = ParseExpression();
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        private Expression ParsePrimaryExpression() {
            if (current.type == SyntaxType.LPAREN) {
                var left = Next();
                var expr = ParseExpression();
                var right = Match(SyntaxType.RPAREN);
                return new ParenExpression(left, expr, right);
            }

            var token = Match(SyntaxType.NUMBER);
            return new LiteralExpression(token);
        }

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Add(new Diagnostic(DiagnosticType.error, $"unexpected token '{current.type}', expected token of type '{type}'"));
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
