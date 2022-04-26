using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Parser {
        private readonly ImmutableArray<Token> tokens_;
        private int position_;
        private readonly SourceText text_;
        private readonly SyntaxTree syntaxTree_;

        public DiagnosticQueue diagnostics;

        private Token Match(SyntaxType type) {
            if (current.type == type) return Next();
            diagnostics.Push(Error.UnexpectedToken(current.location, current.type, type));
            return new Token(syntaxTree_, type, current.position,
                null, null, ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
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

        public Parser(SyntaxTree syntaxTree) {
            diagnostics = new DiagnosticQueue();
            var tokens = new List<Token>();
            var badTokens = new List<Token>();
            Lexer lexer = new Lexer(syntaxTree);
            Token token;
            text_ = syntaxTree.text;
            syntaxTree_ = syntaxTree;

            do {
                token = lexer.LexNext();

                if (token.type == SyntaxType.BAD_TOKEN) {
                    badTokens.Add(token);
                } else {
                    if (badTokens.Count > 0) {
                        var leadingTrivia = token.leadingTrivia.ToBuilder();
                        var index = 0;

                        foreach (var badToken in badTokens) {
                            foreach (var lt in badToken.leadingTrivia)
                                leadingTrivia.Insert(index++, lt);

                            var trivia = new SyntaxTrivia(
                                syntaxTree, SyntaxType.SKIPPED_TOKEN_TRIVIA, badToken.position, badToken.text);
                            leadingTrivia.Insert(index++, trivia);

                            foreach (var tt in badToken.trailingTrivia)
                                leadingTrivia.Insert(index++, tt);
                        }

                        badTokens.Clear();
                        token = new Token(token.syntaxTree, token.type, token.position,
                            token.text, token.value, leadingTrivia.ToImmutable(), token.trailingTrivia);
                    }

                    tokens.Add(token);
                }
            } while (token.type != SyntaxType.END_OF_FILE_TOKEN);

            tokens_ = tokens.ToImmutableArray();
            diagnostics.Move(lexer.diagnostics);
        }

        public CompilationUnit ParseCompilationUnit() {
            var members = ParseMembers();
            var endOfFile = Match(SyntaxType.END_OF_FILE_TOKEN);
            return new CompilationUnit(syntaxTree_, members, endOfFile);
        }

        private ImmutableArray<Member> ParseMembers() {
            var members = ImmutableArray.CreateBuilder<Member>();

            while (current.type != SyntaxType.END_OF_FILE_TOKEN) {
                var startToken = current;

                var member = ParseMember();
                members.Add(member);

                if (current == startToken)
                    Next();
            }

            return members.ToImmutable();
        }

        private Member ParseMember() {
            if (current.type == SyntaxType.IDENTIFIER_TOKEN && tokens_.Length > 3) {
                bool openBrace = false;

                for (int i=0; i<tokens_.Length-position_; i++) {
                    if (Peek(i).type == SyntaxType.SEMICOLON_TOKEN) break;
                    if (Peek(i).type == SyntaxType.OPEN_BRACE_TOKEN) openBrace = true;
                }

                if (openBrace)
                    return ParseFunctionDeclaration();
            }

            return ParseGlobalStatement();
        }

        private Member ParseFunctionDeclaration() {
            var typeName = Match(SyntaxType.IDENTIFIER_TOKEN);
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var parameters = ParseParameterList();
            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            var body = (BlockStatement)ParseBlockStatement();

            return new FunctionDeclaration(
                syntaxTree_, typeName, identifier, openParenthesis, parameters, closeParenthesis, body);
        }

        private SeparatedSyntaxList<Parameter> ParseParameterList() {
            var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

            var parseNextParameter = true;
            while (parseNextParameter &&
                current.type != SyntaxType.CLOSE_PAREN_TOKEN &&
                current.type != SyntaxType.END_OF_FILE_TOKEN) {
                var expression = ParseParameter();
                nodesAndSeparators.Add(expression);

                if (current.type == SyntaxType.COMMA_TOKEN) {
                    var comma = Match(SyntaxType.COMMA_TOKEN);
                    nodesAndSeparators.Add(comma);
                } else {
                    parseNextParameter = false;
                }
            }

            return new SeparatedSyntaxList<Parameter>(nodesAndSeparators.ToImmutable());
        }

        private Parameter ParseParameter() {
            var typeName = Match(SyntaxType.IDENTIFIER_TOKEN);
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            return new Parameter(syntaxTree_, typeName, identifier);
        }

        private Member ParseGlobalStatement() {
            var statement = ParseStatement();
            return new GlobalStatement(syntaxTree_, statement);
        }

        private Statement ParseStatement() {
            switch (current.type) {
                case SyntaxType.OPEN_BRACE_TOKEN:
                    return ParseBlockStatement();
                case SyntaxType.LET_KEYWORD:
                case SyntaxType.VAR_KEYWORD:
                    return ParseVariableDeclarationStatement();
                case SyntaxType.IF_KEYWORD:
                    return ParseIfStatement();
                case SyntaxType.WHILE_KEYWORD:
                    return ParseWhileStatement();
                case SyntaxType.FOR_KEYWORD:
                    return ParseForStatement();
                case SyntaxType.DO_KEYWORD:
                    return ParseDoWhileStatement();
                case SyntaxType.IDENTIFIER_TOKEN:
                    if (Peek(1).type == SyntaxType.IDENTIFIER_TOKEN)
                        return ParseVariableDeclarationStatement();
                    else goto default;
                case SyntaxType.BREAK_KEYWORD:
                    return ParseBreakStatement();
                case SyntaxType.CONTINUE_KEYWORD:
                    return ParseContinueStatement();
                case SyntaxType.RETURN_KEYWORD:
                    return ParseReturnStatement();
                default:
                    return ParseExpressionStatement();
            }
        }

        private Statement ParseReturnStatement() {
            var keyword = Match(SyntaxType.RETURN_KEYWORD);
            Expression expression = null;
            if (current.type != SyntaxType.SEMICOLON_TOKEN)
                expression = ParseExpression();

            Token semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

            return new ReturnStatement(syntaxTree_, keyword, expression, semicolon);
        }

        private Statement ParseContinueStatement() {
            var keyword = Match(SyntaxType.CONTINUE_KEYWORD);
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);
            return new ContinueStatement(syntaxTree_, keyword, semicolon);
        }

        private Statement ParseBreakStatement() {
            var keyword = Match(SyntaxType.BREAK_KEYWORD);
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);
            return new BreakStatement(syntaxTree_, keyword, semicolon);
        }

        private Statement ParseDoWhileStatement() {
            var doKeyword = Match(SyntaxType.DO_KEYWORD);
            var body = ParseStatement();
            var whileKeyword = Match(SyntaxType.WHILE_KEYWORD);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

            return new DoWhileStatement(
                syntaxTree_, doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon);
        }

        private Statement ParseVariableDeclarationStatement() {
            var typeName = Match(current.type);
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            var equals = Match(SyntaxType.EQUALS_TOKEN);
            var initializer = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

            return new VariableDeclarationStatement(syntaxTree_, typeName, identifier, equals, initializer, semicolon);
        }

        private Statement ParseWhileStatement() {
            var keyword = Match(SyntaxType.WHILE_KEYWORD);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            var body = ParseStatement();

            return new WhileStatement(syntaxTree_, keyword, openParenthesis, condition, closeParenthesis, body);
        }

        private Statement ParseForStatement() {
            var keyword = Match(SyntaxType.FOR_KEYWORD);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);

            var initializer = ParseStatement();
            var condition = ParseExpression();
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

            Expression step = null;
            if (current.type == SyntaxType.CLOSE_PAREN_TOKEN)
                step = new EmptyExpression(syntaxTree_);
            else
                step = ParseExpression();

            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            var body = ParseStatement();

            return new ForStatement(
                syntaxTree_, keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body);
        }

        private Statement ParseIfStatement() {
            var keyword = Match(SyntaxType.IF_KEYWORD);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var condition = ParseExpression();
            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            var statement = ParseStatement();

            // not allow nested if statements with else clause without braces; prevents ambiguous else statements
            bool nestedIf = false;
            List<TextLocation> invalidElseLocations = new List<TextLocation>();
            var inter = statement;
            while (inter.type == SyntaxType.IF_STATEMENT) {
                nestedIf = true;
                var interIf = (IfStatement)inter;

                if (interIf.elseClause != null && interIf.then.type != SyntaxType.BLOCK)
                    invalidElseLocations.Add(interIf.elseClause.elseKeyword.location);

                if (interIf.then.type == SyntaxType.IF_STATEMENT)
                    inter = interIf.then;
                else break;
            }
            var elseClause = ParseElseClause();
            if (elseClause != null && statement.type != SyntaxType.BLOCK && nestedIf)
                invalidElseLocations.Add(elseClause.elseKeyword.location);

            while (invalidElseLocations.Count > 0) {
                diagnostics.Push(Error.AmbiguousElse(invalidElseLocations[0]));
                invalidElseLocations.RemoveAt(0);
            }

            return new IfStatement(
                syntaxTree_, keyword, openParenthesis, condition, closeParenthesis, statement, elseClause);
        }

        private ElseClause ParseElseClause() {
            if (current.type != SyntaxType.ELSE_KEYWORD) return null;

            var keyword = Next();
            var statement = ParseStatement();
            return new ElseClause(syntaxTree_, keyword, statement);
        }

        private Statement ParseBlockStatement() {
            var statements = ImmutableArray.CreateBuilder<Statement>();
            var openBrace = Match(SyntaxType.OPEN_BRACE_TOKEN);
            var startToken = current;

            while (current.type != SyntaxType.END_OF_FILE_TOKEN && current.type != SyntaxType.CLOSE_BRACE_TOKEN) {
                var statement = ParseStatement();
                statements.Add(statement);

                if (current == startToken) Next();
                startToken = current;
            }

            var closeBrace = Match(SyntaxType.CLOSE_BRACE_TOKEN);

            return new BlockStatement(syntaxTree_, openBrace, statements.ToImmutable(), closeBrace);
        }

        private Statement ParseExpressionStatement() {
            int previousCount = diagnostics.count;
            var expression = ParseExpression();
            bool popLast = previousCount != diagnostics.count;
            previousCount = diagnostics.count;
            var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);
            popLast = popLast && previousCount != diagnostics.count;

            if (popLast) diagnostics.RemoveAt(diagnostics.count - 1);
            return new ExpressionStatement(syntaxTree_, expression, semicolon);
        }

        private Expression ParseAssignmentExpression() {
            if (Peek(0).type == SyntaxType.IDENTIFIER_TOKEN) {
                switch (Peek(1).type) {
                    case SyntaxType.PLUS_EQUALS_TOKEN:
                    case SyntaxType.MINUS_EQUALS_TOKEN:
                    case SyntaxType.ASTERISK_EQUALS_TOKEN:
                    case SyntaxType.SLASH_EQUALS_TOKEN:
                    case SyntaxType.AMPERSAND_EQUALS_TOKEN:
                    case SyntaxType.PIPE_EQUALS_TOKEN:
                    case SyntaxType.CARET_EQUALS_TOKEN:
                    case SyntaxType.EQUALS_TOKEN:
                        var identifierToken = Next();
                        var operatorToken = Next();
                        var right = ParseAssignmentExpression();
                        return new AssignmentExpression(syntaxTree_, identifierToken, operatorToken, right);
                }
            }

            return ParseBinaryExpression();
        }

        private Expression ParseExpression() {
            if (current.type == SyntaxType.SEMICOLON_TOKEN)
                return ParseEmptyExpression();

            return ParseAssignmentExpression();
        }

        private Expression ParseEmptyExpression() {
            return new EmptyExpression(syntaxTree_);
        }

        private Expression ParseBinaryExpression(int parentPrecedence = 0) {
            Expression left;
            var unaryPrecedence = current.type.GetUnaryPrecedence();

            if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
                var op = Next();
                var operand = ParseBinaryExpression(unaryPrecedence);
                left = new UnaryExpression(syntaxTree_, op, operand);
            } else left = ParsePrimaryExpression();

            while (true) {
                int precedence = current.type.GetBinaryPrecedence();
                if (precedence == 0 || precedence <= parentPrecedence) break;
                var op = Next();
                var right = ParseBinaryExpression(precedence);
                left = new BinaryExpression(syntaxTree_, left, op, right);
            }

            return left;
        }

        private Expression ParsePrimaryExpression() {
            switch (current.type) {
                case SyntaxType.OPEN_PAREN_TOKEN:
                    return ParseParenExpression();
                case SyntaxType.TRUE_KEYWORD:
                case SyntaxType.FALSE_KEYWORD:
                    return ParseBooleanLiteral();
                case SyntaxType.NUMBERIC_LITERAL_TOKEN:
                    return ParseNumberLiteral();
                case SyntaxType.STRING_LITERAL_TOKEN:
                    return ParseStringLiteral();
                case SyntaxType.NAME_EXPRESSION:
                default:
                    return ParseNameOrCallExpression();
            }
        }

        private Expression ParseNumberLiteral() {
            var token = Match(SyntaxType.NUMBERIC_LITERAL_TOKEN);
            return new LiteralExpression(syntaxTree_, token);
        }

        private Expression ParseParenExpression() {
            var left = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var expression = ParseExpression();
            var right = Match(SyntaxType.CLOSE_PAREN_TOKEN);
            return new ParenthesisExpression(syntaxTree_, left, expression, right);
        }

        private Expression ParseBooleanLiteral() {
            var isTrue = current.type == SyntaxType.TRUE_KEYWORD;
            var keyword = isTrue ? Match(SyntaxType.TRUE_KEYWORD) : Match(SyntaxType.FALSE_KEYWORD);
            return new LiteralExpression(syntaxTree_, keyword, isTrue);
        }

        private Expression ParseStringLiteral() {
            var stringToken = Match(SyntaxType.STRING_LITERAL_TOKEN);
            return new LiteralExpression(syntaxTree_, stringToken);
        }

        private Expression ParseNameOrCallExpression() {
            if (Peek(0).type == SyntaxType.IDENTIFIER_TOKEN && Peek(1).type == SyntaxType.OPEN_PAREN_TOKEN)
                return ParseCallExpression();

            return ParseNameExpression();
        }

        private Expression ParseCallExpression() {
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
            var arguments = ParseArguments();
            var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);

            return new CallExpression(syntaxTree_, identifier, openParenthesis, arguments, closeParenthesis);
        }

        private SeparatedSyntaxList<Expression> ParseArguments() {
            var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

            var parseNextArgument = true;
            while (parseNextArgument &&
                current.type != SyntaxType.CLOSE_PAREN_TOKEN &&
                current.type != SyntaxType.END_OF_FILE_TOKEN) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);

                if (current.type == SyntaxType.COMMA_TOKEN) {
                    var comma = Match(SyntaxType.COMMA_TOKEN);
                    nodesAndSeparators.Add(comma);
                } else {
                    parseNextArgument = false;
                }
            }

            return new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
        }

        private Expression ParseNameExpression() {
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            return new NameExpression(syntaxTree_, identifier);
        }
    }
}
