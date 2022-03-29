using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Parser {
        private readonly ImmutableArray<Token> tokens_;
        private int pos_;
        private readonly SourceText text_;
        public DiagnosticQueue diagnostics;

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

        public Parser(SourceText text) {
            diagnostics = new DiagnosticQueue();
            var tokens = new List<Token>();
            Lexer lexer = new Lexer(text);
            Token token;
            text_ = text;

            do {
                token = lexer.LexNext();

                if (token.type != SyntaxType.WHITESPACE && token.type != SyntaxType.Invalid)
                    tokens.Add(token);
            } while (token.type != SyntaxType.EOF);

            tokens_ = tokens.ToImmutableArray();
            diagnostics.Move(lexer.diagnostics);
        }

        public CompilationUnit ParseCompilationUnit() {
            var statements = ImmutableArray.CreateBuilder<Statement>();

            while (current.type != SyntaxType.EOF) {
                var statement = ParseStatement();
                statements.Add(statement);
            }

            var eof = Match(SyntaxType.EOF);
            return new CompilationUnit(statements.ToImmutable(), eof);
        }

        private Statement ParseStatement() {
            switch (current.type) {
                case SyntaxType.LBRACE:
                    return ParseBlockStatement();
                case SyntaxType.LET_KEYWORD:
                case SyntaxType.AUTO_KEYWORD:
                    return ParseVariableDeclaration();
                default:
                    return ParseExpressionStatement();
            }
        }

        private Statement ParseVariableDeclaration() {
            var expected = current.type == SyntaxType.LET_KEYWORD ? SyntaxType.LET_KEYWORD : SyntaxType.AUTO_KEYWORD;
            var keyword = Match(expected);
            var id = Match(SyntaxType.IDENTIFIER);
            var equals = Match(SyntaxType.EQUALS);
            var init = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON);

            return new VariableDeclaration(keyword, id, equals, init, semicolon);
        }

        private Statement ParseBlockStatement() {
            var statements = ImmutableArray.CreateBuilder<Statement>();
            var lbrace = Match(SyntaxType.LBRACE);

            while (current.type != SyntaxType.EOF && current.type != SyntaxType.RBRACE) {
                var statement = ParseStatement();
                statements.Add(statement);
            }

            var rbrace = Match(SyntaxType.RBRACE);

            return new BlockStatement(lbrace, statements.ToImmutable(), rbrace);
        }

        private Statement ParseExpressionStatement() {
            var expression = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON);
            return new ExpressionStatement(expression, semicolon);
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
                    return ParseParenExpression();
                case SyntaxType.TRUE_KEYWORD:
                case SyntaxType.FALSE_KEYWORD:
                    return ParseBooleanLiteral();
                case SyntaxType.NUMBER:
                    return ParseNumberLiteral();
                case SyntaxType.NAME_EXPR:
                default:
                    return ParseNameExpression();
            }
        }

        private Expression ParseNumberLiteral() {
            var token = Match(SyntaxType.NUMBER);
            return new LiteralExpression(token);
        }

        private Expression ParseParenExpression() {
            var left = Match(SyntaxType.LPAREN);
            var expr = ParseExpression();
            var right = Match(SyntaxType.RPAREN);
            return new ParenExpression(left, expr, right);
        }

        private Expression ParseBooleanLiteral() {
            var istrue = current.type == SyntaxType.TRUE_KEYWORD;
            var keyword = istrue ? Match(SyntaxType.TRUE_KEYWORD) : Match(SyntaxType.FALSE_KEYWORD);
            return new LiteralExpression(keyword, istrue);
        }

        private Expression ParseNameExpression() {
            var id = Match(SyntaxType.IDENTIFIER);
            return new NameExpression(id);
        }
    }
}
