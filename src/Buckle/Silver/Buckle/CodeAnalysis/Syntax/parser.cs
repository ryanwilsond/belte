using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Parser {
        private readonly ImmutableArray<Token> tokens_;
        private int position_;
        private readonly SourceText text_;
        public DiagnosticQueue diagnostics;

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Push(Error.UnexpectedToken(current.span, current.type, type));
            return new Token(type, current.position, null, null);
        }

        private Token Next() {
            Token cur = current;
            position_++;
            return cur;
        }

        private Token Peek(int offset) {
            int index = position_ + offset;
            if (index >= tokens_.Length) return tokens_[tokens_.Length - 1];
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

            var endOfFile = Match(SyntaxType.EOF);
            return new CompilationUnit(statements.ToImmutable(), endOfFile);
        }

        private Statement ParseStatement() {
            switch (current.type) {
                case SyntaxType.LBRACE:
                    return ParseBlockStatement();
                case SyntaxType.LET_KEYWORD:
                case SyntaxType.AUTO_KEYWORD:
                case SyntaxType.STRING_KEYWORD:
                case SyntaxType.INT_KEYWORD:
                case SyntaxType.BOOL_KEYWORD:
                    return ParseVariableDeclarationStatement();
                case SyntaxType.IF_KEYWORD:
                    return ParseIfStatement();
                case SyntaxType.WHILE_KEYWORD:
                    return ParseWhileStatement();
                case SyntaxType.FOR_KEYWORD:
                    return ParseForStatement();
                case SyntaxType.DO_KEYWORD:
                    return ParseDoWhileStatement();
                default:
                    return ParseExpressionStatement();
            }
        }

        private Statement ParseDoWhileStatement() {
            var doKeyword = Match(SyntaxType.DO_KEYWORD);
            var body = ParseStatement();
            var whileKeyword = Match(SyntaxType.WHILE_KEYWORD);
            var openParenthesis = Match(SyntaxType.LPAREN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.RPAREN);
            var semicolon = Match(SyntaxType.SEMICOLON);

            return new DoWhileStatement(
                doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon);
        }

        private Statement ParseVariableDeclarationStatement() {
            var keyword = Match(current.type);
            var identifier = Match(SyntaxType.IDENTIFIER);
            var equals = Match(SyntaxType.EQUALS);
            var initializer = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON);

            return new VariableDeclarationStatement(keyword, identifier, equals, initializer, semicolon);
        }

        private Statement ParseWhileStatement() {
            var keyword = Match(SyntaxType.WHILE_KEYWORD);
            var openParenthesis = Match(SyntaxType.LPAREN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.RPAREN);
            var body = ParseStatement();

            return new WhileStatement(keyword, openParenthesis, condition, closeParenthesis, body);
        }

        private Statement ParseForStatement() {
            var keyword = Match(SyntaxType.FOR_KEYWORD);
            var openParenthesis = Match(SyntaxType.LPAREN);

            var initializer = ParseStatement();
            var condition = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON);

            Expression step = null;
            if (current.type == SyntaxType.RPAREN)
                step = new EmptyExpression();
            else
                step = ParseExpression();

            var closeParenthesis = Match(SyntaxType.RPAREN);
            var body = ParseStatement();

            return new ForStatement(
                keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body);
        }

        private Statement ParseIfStatement() {
            var keyword = Match(SyntaxType.IF_KEYWORD);
            var openParenthesis = Match(SyntaxType.LPAREN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.RPAREN);
            var statement = ParseStatement();

            // not allow nested if statements with else clause without braces
            bool nestedIf = false;
            List<TextSpan> invalidElseSpans = new List<TextSpan>();
            var inter = statement;
            while (inter.type == SyntaxType.IF_STATEMENT) {
                nestedIf = true;
                var interIf = (IfStatement)inter;

                if (interIf.elseClause != null && interIf.then.type != SyntaxType.BLOCK_STATEMENT)
                    invalidElseSpans.Add(interIf.elseClause.elseKeyword.span);

                if (interIf.then.type == SyntaxType.IF_STATEMENT)
                    inter = interIf.then;
                else break;
            }
            var elseClause = ParseElseClause();
            if (elseClause != null && statement.type != SyntaxType.BLOCK_STATEMENT && nestedIf)
                invalidElseSpans.Add(elseClause.elseKeyword.span);

            while (invalidElseSpans.Count > 0) {
                diagnostics.Push(Error.AmbiguousElse(invalidElseSpans[0]));
                invalidElseSpans.RemoveAt(0);
            }

            return new IfStatement(keyword, openParenthesis, condition, closeParenthesis, statement, elseClause);
        }

        private ElseClause ParseElseClause() {
            if (current.type != SyntaxType.ELSE_KEYWORD) return null;

            var keyword = Next();
            var statement = ParseStatement();
            return new ElseClause(keyword, statement);
        }

        private Statement ParseBlockStatement() {
            var statements = ImmutableArray.CreateBuilder<Statement>();
            var openBrace = Match(SyntaxType.LBRACE);
            var startToken = current;

            while (current.type != SyntaxType.EOF && current.type != SyntaxType.RBRACE) {
                var statement = ParseStatement();
                statements.Add(statement);

                if (current == startToken) Next();
                startToken = current;
            }

            var closeBrace = Match(SyntaxType.RBRACE);

            return new BlockStatement(openBrace, statements.ToImmutable(), closeBrace);
        }

        private Statement ParseExpressionStatement() {
            int previousCount = diagnostics.count;
            var expression = ParseExpression();
            bool popLast = previousCount != diagnostics.count;
            previousCount = diagnostics.count;
            var semicolon = Match(SyntaxType.SEMICOLON);
            popLast = popLast && previousCount != diagnostics.count;

            if (popLast) diagnostics.RemoveAt(diagnostics.count - 1);
            return new ExpressionStatement(expression, semicolon);
        }

        private Expression ParseAssignmentExpression() {
            if (Peek(0).type == SyntaxType.IDENTIFIER && Peek(1).type == SyntaxType.EQUALS) {
                var identifier = Next();
                var op = Next();
                var right = ParseAssignmentExpression();
                return new AssignmentExpression(identifier, op, right);
            }

            return ParseBinaryExpression();
        }

        private Expression ParseExpression() {
            if (current.type == SyntaxType.SEMICOLON)
                return ParseEmptyExpression();

            return ParseAssignmentExpression();
        }

        private Expression ParseEmptyExpression() {
            return new EmptyExpression();
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
            switch (current.type) {
                case SyntaxType.LPAREN:
                    return ParseParenExpression();
                case SyntaxType.TRUE_KEYWORD:
                case SyntaxType.FALSE_KEYWORD:
                    return ParseBooleanLiteral();
                case SyntaxType.NUMBER:
                    return ParseNumberLiteral();
                case SyntaxType.STRING:
                    return ParseStringLiteral();
                case SyntaxType.NAME_EXPR:
                default:
                    return ParseNameOrCallExpression();
            }
        }

        private Expression ParseNumberLiteral() {
            var token = Match(SyntaxType.NUMBER);
            return new LiteralExpression(token);
        }

        private Expression ParseParenExpression() {
            var left = Match(SyntaxType.LPAREN);
            var expression = ParseExpression();
            var right = Match(SyntaxType.RPAREN);
            return new ParenExpression(left, expression, right);
        }

        private Expression ParseBooleanLiteral() {
            var isTrue = current.type == SyntaxType.TRUE_KEYWORD;
            var keyword = isTrue ? Match(SyntaxType.TRUE_KEYWORD) : Match(SyntaxType.FALSE_KEYWORD);
            return new LiteralExpression(keyword, isTrue);
        }

        private Expression ParseStringLiteral() {
            var stringToken = Match(SyntaxType.STRING);
            return new LiteralExpression(stringToken);
        }

        private Expression ParseNameOrCallExpression() {
            if (Peek(0).type == SyntaxType.IDENTIFIER && Peek(1).type == SyntaxType.LPAREN)
                return ParseCallExpression();

            return ParseNameExpression();
        }

        private Expression ParseCallExpression() {
            var identifier = Match(SyntaxType.IDENTIFIER);
            var openParenthesis = Match(SyntaxType.LPAREN);
            var arguments = ParseArguments();
            var closeParenthesis = Match(SyntaxType.RPAREN);

            return new CallExpression(identifier, openParenthesis, arguments, closeParenthesis);
        }

        private SeparatedSyntaxList<Expression> ParseArguments() {
            var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

            while (current.type != SyntaxType.RPAREN && current.type != SyntaxType.EOF) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);

                if (current.type != SyntaxType.RPAREN) {
                    var comma = Match(SyntaxType.COMMA);
                    nodesAndSeparators.Add(comma);
                }
            }

            return new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
        }

        private Expression ParseNameExpression() {
            var identifier = Match(SyntaxType.IDENTIFIER);
            return new NameExpression(identifier);
        }
    }
}
