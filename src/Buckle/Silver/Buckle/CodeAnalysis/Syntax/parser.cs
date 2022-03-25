using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {

    internal class Parser {
        private readonly Token[] tokens_;
        private int pos_;
        public DiagnosticQueue diagnostics;

        public Parser(string text) {
            diagnostics = new DiagnosticQueue();
            var tokens = new List<Token>();
            Lexer lexer = new Lexer(text);
            Token token;

            do {
                token = lexer.LexNext();

                if (token.type != SyntaxType.WHITESPACE && token.type != SyntaxType.Invalid)
                    tokens.Add(token);
            } while (token.type != SyntaxType.EOF);

            tokens_ = tokens.ToArray();
            diagnostics.Move(lexer.diagnostics);
        }

        public SyntaxTree Parse() {
            var expr = ParseExpression();
            var eof = Match(SyntaxType.EOF);
            return new SyntaxTree(expr, eof, diagnostics);
        }

        private Expression ParseAssignmentExpression() {
            if (Peek(0).type == SyntaxType.IDENTIFIER && Peek(1).type == SyntaxType.EQUALS) {
                var id = Next();
                var op = Next();
                var right = ParseAssignmentExpression();
                return new AssignmentExpression(id, op, right);
            }

            return ParseBinaryExpression();
        }

        private Expression ParseExpression() {
            return ParseAssignmentExpression();
        }

        private Expression ParseBinaryExpression(int parentPrecedence = 0) {
            Expression left;
            var unaryPrecedence = current.type.GetUnaryPrecedence();

            if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
                var op = Next();
                var operand = ParseBinaryExpression(unaryPrecedence);
                left = new UnaryExpression(op, operand);
            } else left = ParsePrimaryExpression();

            while (true) {
                int precedence = current.type.GetBinaryPrecedence();
                if (precedence == 0 || precedence <= parentPrecedence) break;
                var op = Next();
                var right = ParseBinaryExpression(precedence);
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        private Expression ParsePrimaryExpression() {
            switch(current.type) {
                case SyntaxType.LPAREN:
                    var left = Next();
                    var expr = ParseExpression();
                    var right = Match(SyntaxType.RPAREN);
                    return new ParenExpression(left, expr, right);
                case SyntaxType.TRUE_KEYWORD:
                case SyntaxType.FALSE_KEYWORD:
                    var keyword = Next();
                    var value = keyword.type == SyntaxType.TRUE_KEYWORD;
                    return new LiteralExpression(keyword, value);
                case SyntaxType.IDENTIFIER:
                    var id = Next();
                    return new NameExpression(id);
                default:
                    var token = Match(SyntaxType.NUMBER);
                    return new LiteralExpression(token);
            }
        }

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Push(Error.UnexpectedToken(current.span, current.type, type));
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
