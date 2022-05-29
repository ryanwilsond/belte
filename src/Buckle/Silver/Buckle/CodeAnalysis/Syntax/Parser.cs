using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using System;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class Parser {
    private readonly ImmutableArray<Token> tokens_;
    private int position_;
    private readonly SourceText text_;
    private readonly SyntaxTree syntaxTree_;

    public DiagnosticQueue diagnostics;

    private Token Match(SyntaxType type) {
        if (current.type == type)
            return Next();

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

        if (index >= tokens_.Length)
            return tokens_[tokens_.Length - 1];

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

    private bool PeekIsFunctionDeclaration() {
        if (PeekIsTypeClause(out var offset, out var hasName)) {
            if (hasName)
                offset++;

            if (Peek(offset).type == SyntaxType.OPEN_PAREN_TOKEN) {
                while (Peek(offset).type != SyntaxType.END_OF_FILE_TOKEN) {
                    if (Peek(offset).type == SyntaxType.CLOSE_PAREN_TOKEN) {
                        if (Peek(offset+1).type == SyntaxType.OPEN_BRACE_TOKEN)
                            return true;
                        else
                            return false;
                    } else {
                        offset++;
                    }
                }
            }
        }

        return false;
    }

    private bool PeekIsTypeClause(out int offset, out bool hasName) {
        offset = 0;
        hasName = false;

        if (current.type == SyntaxType.IDENTIFIER_TOKEN ||
            current.type == SyntaxType.CONST_KEYWORD ||
            current.type == SyntaxType.REF_KEYWORD ||
            current.type == SyntaxType.VAR_KEYWORD ||
            current.type == SyntaxType.OPEN_BRACKET_TOKEN) {
            while (Peek(offset).type == SyntaxType.OPEN_BRACKET_TOKEN) {
                offset++;

                if (Peek(offset).type == SyntaxType.IDENTIFIER_TOKEN)
                    offset++;
                if (Peek(offset).type == SyntaxType.CLOSE_BRACKET_TOKEN)
                    offset++;
            }

            while (Peek(offset).type == SyntaxType.CONST_KEYWORD ||
                Peek(offset).type == SyntaxType.REF_KEYWORD)
                offset++;

            if (Peek(offset).type == SyntaxType.IDENTIFIER_TOKEN || Peek(offset).type == SyntaxType.VAR_KEYWORD) {
                offset++;

                while (Peek(offset).type == SyntaxType.OPEN_BRACKET_TOKEN ||
                    Peek(offset).type == SyntaxType.CLOSE_BRACKET_TOKEN)
                    offset++;

                if (Peek(offset).type == SyntaxType.IDENTIFIER_TOKEN)
                    hasName = true;

                return true;
            }
        }

        return false;
    }

    private Member ParseMember() {
        if (PeekIsFunctionDeclaration())
            return ParseFunctionDeclaration();

        return ParseGlobalStatement();
    }

    private Member ParseFunctionDeclaration() {
        var typeClause = ParseTypeClause(false);
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
        var body = (BlockStatement)ParseBlockStatement();

        return new FunctionDeclaration(
            syntaxTree_, typeClause, identifier, openParenthesis, parameters, closeParenthesis, body);
    }

    private Statement ParseLocalFunctionDeclaration() {
        var typeClause = ParseTypeClause(false);
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
        var body = (BlockStatement)ParseBlockStatement();

        return new LocalFunctionDeclaration(
            syntaxTree_, typeClause, identifier, openParenthesis, parameters, closeParenthesis, body);
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
        var typeClause = ParseTypeClause(false);
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
        return new Parameter(syntaxTree_, typeClause, identifier);
    }

    private Member ParseGlobalStatement() {
        var statement = ParseStatement();
        return new GlobalStatement(syntaxTree_, statement);
    }

    private Statement ParseStatement() {
        if (PeekIsFunctionDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsTypeClause(out _, out var hasName) && hasName)
            return ParseVariableDeclarationStatement();

        switch (current.type) {
            case SyntaxType.OPEN_BRACE_TOKEN:
                if (!PeekIsInlineFunctionExpression())
                    return ParseBlockStatement();
                else
                    goto default;
            case SyntaxType.IF_KEYWORD:
                return ParseIfStatement();
            case SyntaxType.WHILE_KEYWORD:
                return ParseWhileStatement();
            case SyntaxType.FOR_KEYWORD:
                return ParseForStatement();
            case SyntaxType.DO_KEYWORD:
                return ParseDoWhileStatement();
            case SyntaxType.TRY_KEYWORD:
                return ParseTryStatement();
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

    private Statement ParseTryStatement() {
        var tryKeyword = Match(SyntaxType.TRY_KEYWORD);
        var body = ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause == null && finallyClause == null)
            diagnostics.Push(Error.NoCatchOrFinally(tryKeyword.location));

        return new TryStatement(syntaxTree_, tryKeyword, (BlockStatement)body, catchClause, finallyClause);
    }

    private CatchClause ParseCatchClause() {
        if (current.type != SyntaxType.CATCH_KEYWORD)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();
        return new CatchClause(syntaxTree_, keyword, (BlockStatement)body);
    }

    private FinallyClause ParseFinallyClause() {
        if (current.type != SyntaxType.FINALLY_KEYWORD)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();
        return new FinallyClause(syntaxTree_, keyword, (BlockStatement)body);
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
        var typeClause = ParseTypeClause();
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);

        Token equals = null;
        Expression initializer = null;

        if (current.type == SyntaxType.EQUALS_TOKEN) {
            equals = Match(SyntaxType.EQUALS_TOKEN);
            initializer = ParseExpression();
        }

        var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

        return new VariableDeclarationStatement(
            syntaxTree_, typeClause, identifier, equals, initializer, semicolon);
    }

    private TypeClause ParseTypeClause(bool allowImplicit = true) {
        var attributes = ImmutableArray.CreateBuilder<(Token openBracket, Token identifier, Token closeBracket)>();

        while (current.type == SyntaxType.OPEN_BRACKET_TOKEN) {
            var openBracket = Match(SyntaxType.OPEN_BRACKET_TOKEN);
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            var closeBracket = Match(SyntaxType.CLOSE_BRACKET_TOKEN);
            attributes.Add((openBracket, identifier, closeBracket));
        }

        Token constRefKeyword = null;
        Token refKeyword = null;
        Token constKeyword = null;
        Token typeName = null;

        if (current.type == SyntaxType.CONST_KEYWORD && Peek(1).type == SyntaxType.REF_KEYWORD)
            constRefKeyword = Match(SyntaxType.CONST_KEYWORD);
        if (current.type == SyntaxType.REF_KEYWORD)
            refKeyword = Match(SyntaxType.REF_KEYWORD);
        if (current.type == SyntaxType.CONST_KEYWORD)
            constKeyword = Match(SyntaxType.CONST_KEYWORD);

        if (current.type == SyntaxType.VAR_KEYWORD) {
            typeName = Match(SyntaxType.VAR_KEYWORD);

            if (!allowImplicit)
                diagnostics.Push(Error.CannotUseImplicit(typeName.location));
        } else {
            typeName = Match(SyntaxType.IDENTIFIER_TOKEN);
        }

        var brackets = ImmutableArray.CreateBuilder<(Token openBracket, Token closeBracket)>();

        while (current.type == SyntaxType.OPEN_BRACKET_TOKEN) {
            var openBracket = Match(SyntaxType.OPEN_BRACKET_TOKEN);
            var closeBracket = Match(SyntaxType.CLOSE_BRACKET_TOKEN);
            brackets.Add((openBracket, closeBracket));
        }

        return new TypeClause(
            syntaxTree_, attributes.ToImmutable(), constRefKeyword,
            refKeyword, constKeyword, typeName, brackets.ToImmutable());
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
            else
                break;
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
        if (current.type != SyntaxType.ELSE_KEYWORD)
            return null;

        var keyword = Next();
        var statement = ParseStatement();
        return new ElseClause(syntaxTree_, keyword, statement);
    }

    private bool PeekIsInlineFunctionExpression() {
        var offset = 1;
        var stack = 1;

        if (current.type != SyntaxType.OPEN_BRACE_TOKEN)
            return false;

        while (Peek(offset).type != SyntaxType.END_OF_FILE_TOKEN && stack > 0) {
            if (Peek(offset).type == SyntaxType.RETURN_KEYWORD)
                return true;
            else if (Peek(offset).type == SyntaxType.OPEN_BRACE_TOKEN)
                stack++;
            else if (Peek(offset).type == SyntaxType.CLOSE_BRACE_TOKEN)
                stack--;

            offset++;
        }

        return false;
    }

    private Statement ParseBlockStatement() {
        return (Statement)ParseBlockStatementOrInlineFunctionExpression(true);
    }

    private Expression ParseInlineFunctionExpression() {
        var node = ParseBlockStatementOrInlineFunctionExpression(false);

        if (node.type == SyntaxType.INLINE_FUNCTION) {
            return (Expression)node;
        } else {
            diagnostics.Push(Error.MissingReturnStatement(((BlockStatement)node).closeBrace.location));
            return null;
        }
    }

    private Node ParseBlockStatementOrInlineFunctionExpression(bool isBlock = false) {
        var statements = ImmutableArray.CreateBuilder<Statement>();
        var openBrace = Match(SyntaxType.OPEN_BRACE_TOKEN);
        var startToken = current;

        while (current.type != SyntaxType.END_OF_FILE_TOKEN && current.type != SyntaxType.CLOSE_BRACE_TOKEN) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (current == startToken)
                Next();

            startToken = current;
        }

        var closeBrace = Match(SyntaxType.CLOSE_BRACE_TOKEN);

        if (isBlock)
            return new BlockStatement(syntaxTree_, openBrace, statements.ToImmutable(), closeBrace);
        else
            return new InlineFunctionExpression(syntaxTree_, openBrace, statements.ToImmutable(), closeBrace);
    }

    private Statement ParseExpressionStatement() {
        int previousCount = diagnostics.count;
        var expression = ParseExpression();
        bool popLast = previousCount != diagnostics.count;
        previousCount = diagnostics.count;
        var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);
        popLast = popLast && previousCount != diagnostics.count;

        if (popLast)
            diagnostics.RemoveAt(diagnostics.count - 1);

        return new ExpressionStatement(syntaxTree_, expression, semicolon);
    }

    private Expression ParseAssignmentExpression() {
        if (current.type == SyntaxType.IDENTIFIER_TOKEN) {
            switch (Peek(1).type) {
                case SyntaxType.PLUS_EQUALS_TOKEN:
                case SyntaxType.MINUS_EQUALS_TOKEN:
                case SyntaxType.ASTERISK_EQUALS_TOKEN:
                case SyntaxType.SLASH_EQUALS_TOKEN:
                case SyntaxType.AMPERSAND_EQUALS_TOKEN:
                case SyntaxType.PIPE_EQUALS_TOKEN:
                case SyntaxType.CARET_EQUALS_TOKEN:
                case SyntaxType.ASTERISK_ASTERISK_EQUALS_TOKEN:
                case SyntaxType.LESS_THAN_LESS_THAN_EQUALS_TOKEN:
                case SyntaxType.GREATER_THAN_GREATER_THAN_EQUALS_TOKEN:
                case SyntaxType.EQUALS_TOKEN:
                    var identifierToken = Next();
                    var operatorToken = Next();
                    var right = ParseAssignmentExpression();
                    return new AssignmentExpression(syntaxTree_, identifierToken, operatorToken, right);
                default:
                    break;
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

            if (op.type == SyntaxType.PLUS_PLUS_TOKEN || op.type == SyntaxType.MINUS_MINUS_TOKEN) {
                var operand = Match(SyntaxType.IDENTIFIER_TOKEN);
                left = new PrefixExpression(syntaxTree_, op, operand);
            } else {
                var operand = ParseBinaryExpression(unaryPrecedence);
                left = new UnaryExpression(syntaxTree_, op, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            int precedence = current.type.GetBinaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

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
            case SyntaxType.NUMERIC_LITERAL_TOKEN:
                return ParseNumericLiteral();
            case SyntaxType.STRING_LITERAL_TOKEN:
                return ParseStringLiteral();
            case SyntaxType.NULL_KEYWORD:
                return ParseNullLiteral();
            case SyntaxType.OPEN_BRACE_TOKEN:
                if (PeekIsInlineFunctionExpression())
                    return ParseInlineFunctionExpression();
                else
                    return ParseInitializerListExpression();
            case SyntaxType.REF_KEYWORD:
                return ParseReferenceExpression();
            case SyntaxType.IDENTIFIER_TOKEN:
                if (Peek(1).type == SyntaxType.PLUS_PLUS_TOKEN || Peek(1).type == SyntaxType.MINUS_MINUS_TOKEN)
                    return ParsePostfixExpression();
                else
                    goto default;
            case SyntaxType.NAME_EXPRESSION:
            default:
                return ParseNameOrPrimaryOperatorExpression();
        }
    }

    private Expression ParseReferenceExpression() {
        var refKeyword = Match(SyntaxType.REF_KEYWORD);
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);

        return new ReferenceExpression(syntaxTree_, refKeyword, identifier);
    }

    private Expression ParsePostfixExpression() {
        var operand = Match(SyntaxType.IDENTIFIER_TOKEN);
        Token op = null;

        if (current.type == SyntaxType.MINUS_MINUS_TOKEN)
            op = Match(SyntaxType.MINUS_MINUS_TOKEN);
        if (current.type == SyntaxType.PLUS_PLUS_TOKEN)
            op = Match(SyntaxType.PLUS_PLUS_TOKEN);

        return new PostfixExpression(syntaxTree_, operand, op);
    }

    private Expression ParseInitializerListExpression() {
        var left = Match(SyntaxType.OPEN_BRACE_TOKEN);
        var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

        var parseNextItem = true;
        while (parseNextItem &&
            current.type != SyntaxType.CLOSE_BRACE_TOKEN &&
            current.type != SyntaxType.END_OF_FILE_TOKEN) {
            var expression = ParseExpression();
            nodesAndSeparators.Add(expression);

            if (current.type == SyntaxType.COMMA_TOKEN) {
                var comma = Match(SyntaxType.COMMA_TOKEN);
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
        var right = Match(SyntaxType.CLOSE_BRACE_TOKEN);
        return new InitializerListExpression(syntaxTree_, left, separatedSyntaxList, right);
    }

    private Expression ParseNullLiteral() {
        var token = Match(SyntaxType.NULL_KEYWORD);
        return new LiteralExpression(syntaxTree_, token);
    }

    private Expression ParseNumericLiteral() {
        var token = Match(SyntaxType.NUMERIC_LITERAL_TOKEN);
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

    private Expression ParsePrimaryOperatorExpression(Expression operand, int parentPrecedence = 0) {
        Expression ParseCorrectPrimaryOperator(Expression operand) {
            if (current.type == SyntaxType.OPEN_PAREN_TOKEN)
                return ParseCallExpression(operand);
            if (current.type == SyntaxType.OPEN_BRACKET_TOKEN)
                return ParseIndexExpression(operand);

            return operand;
        }

        while (true) {
            var precedence = current.type.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            var expression = ParseCorrectPrimaryOperator(operand);
            operand = ParsePrimaryOperatorExpression(expression, precedence);
        }

        return operand;
    }

    private Expression ParseNameOrPrimaryOperatorExpression() {
        var left = ParseNameExpression();
        return ParsePrimaryOperatorExpression(left);
    }

    private Expression ParseIndexExpression(Expression operand) {
        var openBracket = Match(SyntaxType.OPEN_BRACKET_TOKEN);
        var index = ParseExpression();
        var closeBracket = Match(SyntaxType.CLOSE_BRACKET_TOKEN);

        return new IndexExpression(syntaxTree_, operand, openBracket, index, closeBracket);
    }

    private Expression ParseCallExpression(Expression operand) {
        if (operand.type != SyntaxType.NAME_EXPRESSION) {
            diagnostics.Push(Error.ExpectedMethodName(operand.location));
            return operand;
        }

        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
        var arguments = ParseArguments();
        var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);

        return new CallExpression(syntaxTree_, (NameExpression)operand, openParenthesis, arguments, closeParenthesis);
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
