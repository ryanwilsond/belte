using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using System;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Lexes then parses text into a tree of nodes, in doing so doing syntax checking.
/// </summary>
internal sealed class Parser {
    private readonly ImmutableArray<Token> tokens_;
    private readonly SourceText text_;
    private readonly SyntaxTree syntaxTree_;
    private int position_;

    /// <summary>
    /// Creates a new parser, requiring a fully initialized syntax tree.
    /// </summary>
    /// <param name="syntaxTree">Syntax tree to parse from</param>
    internal Parser(SyntaxTree syntaxTree) {
        diagnostics = new BelteDiagnosticQueue();
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

    /// <summary>
    /// Diagnostics produced during the parsing process.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    private Token current => Peek(0);

    /// <summary>
    /// Parses the entirety of a single file.
    /// </summary>
    /// <returns>The parsed file</returns>
    internal CompilationUnit ParseCompilationUnit() {
        var members = ParseMembers();
        var endOfFile = Match(SyntaxType.END_OF_FILE_TOKEN);
        return new CompilationUnit(syntaxTree_, members, endOfFile);
    }

    private Token Match(SyntaxType type, SyntaxType? nextWanted = null, bool suppressErrors = false) {
        if (current.type == type)
            return Next();

        if (nextWanted != null && current.type == nextWanted) {
            if (!suppressErrors)
                diagnostics.Push(Error.ExpectedToken(current.location, type));

            return new Token(syntaxTree_, type, current.position,
                null, null, ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
        } else if (Peek(1).type != type) {
            if (!suppressErrors)
                diagnostics.Push(Error.UnexpectedToken(current.location, current.type, type));

            Token cur = current;
            position_++;

            return new Token(syntaxTree_, type, cur.position,
                null, null, ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
        } else {
            if (!suppressErrors)
                diagnostics.Push(Error.UnexpectedToken(current.location, current.type));

            position_++;
            Token cur = current;
            position_++;

            return cur;
        }
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
        if (PeekIsTypeClause(0, out var offset, out var hasName)) {
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

    private bool PeekIsTypeClause(int offset, out int finalOffset, out bool hasName) {
        finalOffset = offset;
        hasName = false;

        if (Peek(finalOffset).type == SyntaxType.IDENTIFIER_TOKEN ||
            Peek(finalOffset).type == SyntaxType.CONST_KEYWORD ||
            Peek(finalOffset).type == SyntaxType.REF_KEYWORD ||
            Peek(finalOffset).type == SyntaxType.VAR_KEYWORD ||
            Peek(finalOffset).type == SyntaxType.OPEN_BRACKET_TOKEN) {
            while (Peek(finalOffset).type == SyntaxType.OPEN_BRACKET_TOKEN) {
                finalOffset++;

                if (Peek(finalOffset).type == SyntaxType.IDENTIFIER_TOKEN)
                    finalOffset++;
                if (Peek(finalOffset).type == SyntaxType.CLOSE_BRACKET_TOKEN)
                    finalOffset++;
            }

            while (Peek(finalOffset).type == SyntaxType.CONST_KEYWORD ||
                Peek(finalOffset).type == SyntaxType.REF_KEYWORD)
                finalOffset++;

            if (Peek(finalOffset).type == SyntaxType.IDENTIFIER_TOKEN ||
                Peek(finalOffset).type == SyntaxType.VAR_KEYWORD) {
                finalOffset++;

                while (Peek(finalOffset).type == SyntaxType.OPEN_BRACKET_TOKEN ||
                    Peek(finalOffset).type == SyntaxType.CLOSE_BRACKET_TOKEN)
                    finalOffset++;

                if (Peek(finalOffset).type == SyntaxType.IDENTIFIER_TOKEN)
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
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN, SyntaxType.OPEN_PAREN_TOKEN);
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

            // TODO Optional parameters
            if (current.type == SyntaxType.COMMA_TOKEN) {
                var comma = Next();
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

    private Statement ParseStatement(bool disableInlines = false) {
        if (PeekIsFunctionDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsTypeClause(0, out _, out var hasName) && hasName)
            return ParseVariableDeclarationStatement();

        switch (current.type) {
            case SyntaxType.OPEN_BRACE_TOKEN:
                if (!PeekIsInlineFunctionExpression() || disableInlines)
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
        var body = ParseStatement(true);
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
            equals = Next();
            initializer = ParseExpression();
        }

        var semicolon = Match(SyntaxType.SEMICOLON_TOKEN);

        return new VariableDeclarationStatement(
            syntaxTree_, typeClause, identifier, equals, initializer, semicolon);
    }

    private TypeClause ParseTypeClause(bool allowImplicit = true) {
        var attributes = ImmutableArray.CreateBuilder<(Token openBracket, Token identifier, Token closeBracket)>();

        while (current.type == SyntaxType.OPEN_BRACKET_TOKEN) {
            var openBracket = Next();
            var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);
            var closeBracket = Match(SyntaxType.CLOSE_BRACKET_TOKEN);
            attributes.Add((openBracket, identifier, closeBracket));
        }

        Token constRefKeyword = null;
        Token refKeyword = null;
        Token constKeyword = null;
        Token typeName = null;

        if (current.type == SyntaxType.CONST_KEYWORD && Peek(1).type == SyntaxType.REF_KEYWORD)
            constRefKeyword = Next();
        if (current.type == SyntaxType.REF_KEYWORD)
            refKeyword = Next();
        if (current.type == SyntaxType.CONST_KEYWORD)
            constKeyword = Next();

        if (current.type == SyntaxType.VAR_KEYWORD) {
            typeName = Next();

            if (!allowImplicit)
                diagnostics.Push(Error.CannotUseImplicit(typeName.location));
        } else {
            typeName = Match(SyntaxType.IDENTIFIER_TOKEN);
        }

        var brackets = ImmutableArray.CreateBuilder<(Token openBracket, Token closeBracket)>();

        while (current.type == SyntaxType.OPEN_BRACKET_TOKEN) {
            var openBracket = Next();
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
        var body = ParseStatement(true);

        return new WhileStatement(syntaxTree_, keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private Statement ParseForStatement() {
        var keyword = Match(SyntaxType.FOR_KEYWORD);
        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);

        var initializer = ParseStatement(true);
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
        var statement = ParseStatement(true);

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
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

        var keyword = Match(SyntaxType.ELSE_KEYWORD);
        var statement = ParseStatement(true);
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
            diagnostics.PopBack();

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
                if (PeekIsTypeClause(1, out _, out _))
                    return ParseCastExpression();
                else
                    return ParseParenthesizedExpression();
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
            case SyntaxType.TYPEOF_KEYWORD:
            default:
                return ParseNameOrPrimaryOperatorExpression();
        }
    }

    private Expression ParseCastExpression() {
        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
        var typeClause = ParseTypeClause();
        var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);
        var expression = ParseExpression();

        return new CastExpression(syntaxTree_, openParenthesis, typeClause, closeParenthesis, expression);
    }

    private Expression ParseReferenceExpression() {
        var refKeyword = Match(SyntaxType.REF_KEYWORD);
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN);

        return new ReferenceExpression(syntaxTree_, refKeyword, identifier);
    }

    private Expression ParsePostfixExpression() {
        var operand = Match(SyntaxType.IDENTIFIER_TOKEN);
        Token op = null;

        if (current.type == SyntaxType.MINUS_MINUS_TOKEN || current.type == SyntaxType.PLUS_PLUS_TOKEN)
            op = Next();

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
                var comma = Next();
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

    private Expression ParseParenthesizedExpression() {
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

    private Expression ParsePrimaryOperatorExpression(
        Node operand, int parentPrecedence = 0, Token maybeUnexpected=null) {
        Node ParseCorrectPrimaryOperator(Node operand) {
            if (operand.type == SyntaxType.TYPEOF_KEYWORD)
                return ParseTypeofExpression();
            else if (current.type == SyntaxType.OPEN_PAREN_TOKEN)
                return ParseCallExpression((Expression)operand);
            else if (current.type == SyntaxType.OPEN_BRACKET_TOKEN)
                return ParseIndexExpression((Expression)operand);

            return operand;
        }

        var completeIterations = 0;

        while (true) {
            var startToken = current;
            var precedence = current.type.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            var expression = ParseCorrectPrimaryOperator(operand);
            operand = ParsePrimaryOperatorExpression(expression, precedence);

            completeIterations++;

            if (startToken == current)
                Next();
        }

        if (completeIterations == 0 && operand is NameExpression ne && ne.identifier.isMissing)
            diagnostics.Push(Error.UnexpectedToken(maybeUnexpected.location, maybeUnexpected.type));

        // Assuming that all typeof operators are handled and do not fall through
        return (Expression)operand;
    }

    private Expression ParseTypeofExpression() {
        var typeofKeyword = Next();
        var openParenthesis = Match(SyntaxType.OPEN_PAREN_TOKEN);
        var typeClause = ParseTypeClause(false);
        var closeParenthesis = Match(SyntaxType.CLOSE_PAREN_TOKEN);

        return new TypeofExpression(syntaxTree_, typeofKeyword, openParenthesis, typeClause, closeParenthesis);
    }

    private Expression ParseNameOrPrimaryOperatorExpression() {
        var maybeUnexpected = current;

        Node left;
        if (current.type == SyntaxType.TYPEOF_KEYWORD)
            left = current;
        else
            left = ParseNameExpression(true);

        return ParsePrimaryOperatorExpression(left, maybeUnexpected: maybeUnexpected);
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
            if (current.type != SyntaxType.COMMA_TOKEN) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            }

            if (current.type == SyntaxType.COMMA_TOKEN) {
                var comma = Next();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextArgument = false;
            }
        }

        return new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
    }

    private Expression ParseNameExpression(bool suppressErrors = false) {
        var identifier = Match(SyntaxType.IDENTIFIER_TOKEN, suppressErrors: suppressErrors);
        return new NameExpression(syntaxTree_, identifier);
    }
}
