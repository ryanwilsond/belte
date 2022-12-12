using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Lexes then parses text into a tree of nodes, in doing so doing syntax checking.
/// </summary>
internal sealed class Parser {
    private readonly ImmutableArray<Token> _tokens;
    private readonly SourceText _text;
    private readonly SyntaxTree _syntaxTree;
    private int _position;

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
        _text = syntaxTree.text;
        _syntaxTree = syntaxTree;

        do {
            token = lexer.LexNext();

            if (token.type == SyntaxType.BadToken) {
                badTokens.Add(token);
            } else {
                if (badTokens.Count > 0) {
                    var leadingTrivia = token.leadingTrivia.ToBuilder();
                    var index = 0;

                    foreach (var badToken in badTokens) {
                        foreach (var lt in badToken.leadingTrivia)
                            leadingTrivia.Insert(index++, lt);

                        var trivia = new SyntaxTrivia(
                            syntaxTree, SyntaxType.SkippedTokenTrivia, badToken.position, badToken.text);
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
        } while (token.type != SyntaxType.EndOfFileToken);

        _tokens = tokens.ToImmutableArray();
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
        var endOfFile = Match(SyntaxType.EndOfFileToken);
        return new CompilationUnit(_syntaxTree, members, endOfFile);
    }

    private Token Match(SyntaxType type, SyntaxType? nextWanted = null, bool suppressErrors = false) {
        if (current.type == type)
            return Next();

        if (nextWanted != null && current.type == nextWanted) {
            if (!suppressErrors)
                diagnostics.Push(Error.ExpectedToken(current.location, type));

            return new Token(_syntaxTree, type, current.position,
                null, null, ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
        } else if (Peek(1).type != type) {
            if (!suppressErrors)
                diagnostics.Push(Error.UnexpectedToken(current.location, current.type, type));

            Token cur = current;
            _position++;

            return new Token(_syntaxTree, type, cur.position,
                null, null, ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
        } else {
            if (!suppressErrors)
                diagnostics.Push(Error.UnexpectedToken(current.location, current.type));

            _position++;
            Token cur = current;
            _position++;

            return cur;
        }
    }

    private Token Next() {
        Token cur = current;
        _position++;
        return cur;
    }

    private Token Peek(int offset) {
        int index = _position + offset;

        if (index >= _tokens.Length)
            return _tokens[_tokens.Length - 1];

        return _tokens[index];
    }

    private ImmutableArray<Member> ParseMembers() {
        var members = ImmutableArray.CreateBuilder<Member>();

        while (current.type != SyntaxType.EndOfFileToken) {
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

            if (Peek(offset).type == SyntaxType.OpenParenToken) {
                while (Peek(offset).type != SyntaxType.EndOfFileToken) {
                    if (Peek(offset).type == SyntaxType.CloseParenToken) {
                        if (Peek(offset+1).type == SyntaxType.OpenBraceToken)
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

        if (Peek(finalOffset).type == SyntaxType.IdentifierToken ||
            Peek(finalOffset).type == SyntaxType.ConstKeyword ||
            Peek(finalOffset).type == SyntaxType.RefKeyword ||
            Peek(finalOffset).type == SyntaxType.VarKeyword ||
            Peek(finalOffset).type == SyntaxType.OpenBracketToken) {
            while (Peek(finalOffset).type == SyntaxType.OpenBracketToken) {
                finalOffset++;

                if (Peek(finalOffset).type == SyntaxType.IdentifierToken)
                    finalOffset++;
                if (Peek(finalOffset).type == SyntaxType.CloseBracketToken)
                    finalOffset++;
            }

            while (Peek(finalOffset).type == SyntaxType.ConstKeyword ||
                Peek(finalOffset).type == SyntaxType.RefKeyword)
                finalOffset++;

            if (Peek(finalOffset).type == SyntaxType.IdentifierToken ||
                Peek(finalOffset).type == SyntaxType.VarKeyword) {
                finalOffset++;

                while (Peek(finalOffset).type == SyntaxType.OpenBracketToken ||
                    Peek(finalOffset).type == SyntaxType.CloseBracketToken)
                    finalOffset++;

                if (Peek(finalOffset).type == SyntaxType.IdentifierToken)
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
        var identifier = Match(SyntaxType.IdentifierToken, SyntaxType.OpenParenToken);
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var body = (BlockStatement)ParseBlockStatement();

        return new FunctionDeclaration(
            _syntaxTree, typeClause, identifier, openParenthesis, parameters, closeParenthesis, body);
    }

    private Statement ParseLocalFunctionDeclaration() {
        var typeClause = ParseTypeClause(false);
        var identifier = Match(SyntaxType.IdentifierToken);
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var body = (BlockStatement)ParseBlockStatement();

        return new LocalFunctionStatement(
            _syntaxTree, typeClause, identifier, openParenthesis, parameters, closeParenthesis, body);
    }

    private SeparatedSyntaxList<Parameter> ParseParameterList() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();
        var parseNextParameter = true;

        while (parseNextParameter &&
            current.type != SyntaxType.CloseParenToken &&
            current.type != SyntaxType.EndOfFileToken) {
            var expression = ParseParameter();
            nodesAndSeparators.Add(expression);

            if (current.type == SyntaxType.CommaToken) {
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
        var identifier = Match(SyntaxType.IdentifierToken);
        return new Parameter(_syntaxTree, typeClause, identifier);
    }

    private Member ParseGlobalStatement() {
        var statement = ParseStatement();
        return new GlobalStatement(_syntaxTree, statement);
    }

    private Statement ParseStatement(bool disableInlines = false) {
        if (PeekIsFunctionDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsTypeClause(0, out _, out var hasName) && hasName)
            return ParseVariableDeclarationStatement();

        switch (current.type) {
            case SyntaxType.OpenBraceToken:
                if (!PeekIsInlineFunctionExpression() || disableInlines)
                    return ParseBlockStatement();
                else
                    goto default;
            case SyntaxType.IfKeyword:
                return ParseIfStatement();
            case SyntaxType.WhileKeyword:
                return ParseWhileStatement();
            case SyntaxType.ForKeyword:
                return ParseForStatement();
            case SyntaxType.DoKeyword:
                return ParseDoWhileStatement();
            case SyntaxType.TryKeyword:
                return ParseTryStatement();
            case SyntaxType.BreakKeyword:
                return ParseBreakStatement();
            case SyntaxType.ContinueKeyword:
                return ParseContinueStatement();
            case SyntaxType.ReturnKeyword:
                return ParseReturnStatement();
            default:
                return ParseExpressionStatement();
        }
    }

    private Statement ParseTryStatement() {
        var tryKeyword = Match(SyntaxType.TryKeyword);
        var body = ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause == null && finallyClause == null)
            diagnostics.Push(Error.NoCatchOrFinally(tryKeyword.location));

        return new TryStatement(_syntaxTree, tryKeyword, (BlockStatement)body, catchClause, finallyClause);
    }

    private CatchClause ParseCatchClause() {
        if (current.type != SyntaxType.CatchKeyword)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();
        return new CatchClause(_syntaxTree, keyword, (BlockStatement)body);
    }

    private FinallyClause ParseFinallyClause() {
        if (current.type != SyntaxType.FinallyKeyword)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();
        return new FinallyClause(_syntaxTree, keyword, (BlockStatement)body);
    }

    private Statement ParseReturnStatement() {
        var keyword = Match(SyntaxType.ReturnKeyword);
        Expression expression = null;
        if (current.type != SyntaxType.SemicolonToken)
            expression = ParseExpression();

        Token semicolon = Match(SyntaxType.SemicolonToken);

        return new ReturnStatement(_syntaxTree, keyword, expression, semicolon);
    }

    private Statement ParseContinueStatement() {
        var keyword = Match(SyntaxType.ContinueKeyword);
        var semicolon = Match(SyntaxType.SemicolonToken);
        return new ContinueStatement(_syntaxTree, keyword, semicolon);
    }

    private Statement ParseBreakStatement() {
        var keyword = Match(SyntaxType.BreakKeyword);
        var semicolon = Match(SyntaxType.SemicolonToken);
        return new BreakStatement(_syntaxTree, keyword, semicolon);
    }

    private Statement ParseDoWhileStatement() {
        var doKeyword = Match(SyntaxType.DoKeyword);
        var body = ParseStatement(true);
        var whileKeyword = Match(SyntaxType.WhileKeyword);
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var condition = ParseExpression();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var semicolon = Match(SyntaxType.SemicolonToken);

        return new DoWhileStatement(
            _syntaxTree, doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon);
    }

    private Statement ParseVariableDeclarationStatement() {
        var typeClause = ParseTypeClause();
        var identifier = Match(SyntaxType.IdentifierToken);

        Token equals = null;
        Expression initializer = null;

        if (current.type == SyntaxType.EqualsToken) {
            equals = Next();
            initializer = ParseExpression();
        }

        var semicolon = Match(SyntaxType.SemicolonToken);

        return new VariableDeclarationStatement(
            _syntaxTree, typeClause, identifier, equals, initializer, semicolon);
    }

    private TypeClause ParseTypeClause(bool allowImplicit = true) {
        var attributes = ImmutableArray.CreateBuilder<(Token openBracket, Token identifier, Token closeBracket)>();

        while (current.type == SyntaxType.OpenBracketToken) {
            var openBracket = Next();
            var identifier = Match(SyntaxType.IdentifierToken);
            var closeBracket = Match(SyntaxType.CloseBracketToken);
            attributes.Add((openBracket, identifier, closeBracket));
        }

        Token constRefKeyword = null;
        Token refKeyword = null;
        Token constKeyword = null;
        Token typeName = null;

        if (current.type == SyntaxType.ConstKeyword && Peek(1).type == SyntaxType.RefKeyword)
            constRefKeyword = Next();
        if (current.type == SyntaxType.RefKeyword)
            refKeyword = Next();
        if (current.type == SyntaxType.ConstKeyword)
            constKeyword = Next();

        if (current.type == SyntaxType.VarKeyword) {
            typeName = Next();

            if (!allowImplicit)
                diagnostics.Push(Error.CannotUseImplicit(typeName.location));
        } else {
            typeName = Match(SyntaxType.IdentifierToken);
        }

        var brackets = ImmutableArray.CreateBuilder<(Token openBracket, Token closeBracket)>();

        while (current.type == SyntaxType.OpenBracketToken) {
            var openBracket = Next();
            var closeBracket = Match(SyntaxType.CloseBracketToken);
            brackets.Add((openBracket, closeBracket));
        }

        return new TypeClause(
            _syntaxTree, attributes.ToImmutable(), constRefKeyword,
            refKeyword, constKeyword, typeName, brackets.ToImmutable());
    }

    private Statement ParseWhileStatement() {
        var keyword = Match(SyntaxType.WhileKeyword);
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var condition = ParseExpression();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var body = ParseStatement(true);

        return new WhileStatement(_syntaxTree, keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private Statement ParseForStatement() {
        var keyword = Match(SyntaxType.ForKeyword);
        var openParenthesis = Match(SyntaxType.OpenParenToken);

        var initializer = ParseStatement(true);
        var condition = ParseExpression();
        var semicolon = Match(SyntaxType.SemicolonToken);

        Expression step = null;
        if (current.type == SyntaxType.CloseParenToken)
            step = new EmptyExpression(_syntaxTree);
        else
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var body = ParseStatement();

        return new ForStatement(
            _syntaxTree, keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body);
    }

    private Statement ParseIfStatement() {
        var keyword = Match(SyntaxType.IfKeyword);
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var condition = ParseExpression();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var statement = ParseStatement(true);

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        bool nestedIf = false;
        List<TextLocation> invalidElseLocations = new List<TextLocation>();
        var inter = statement;

        while (inter.type == SyntaxType.IfStatement) {
            nestedIf = true;
            var interIf = (IfStatement)inter;

            if (interIf.elseClause != null && interIf.then.type != SyntaxType.Block)
                invalidElseLocations.Add(interIf.elseClause.elseKeyword.location);

            if (interIf.then.type == SyntaxType.IfStatement)
                inter = interIf.then;
            else
                break;
        }

        var elseClause = ParseElseClause();
        if (elseClause != null && statement.type != SyntaxType.Block && nestedIf)
            invalidElseLocations.Add(elseClause.elseKeyword.location);

        while (invalidElseLocations.Count > 0) {
            diagnostics.Push(Error.AmbiguousElse(invalidElseLocations[0]));
            invalidElseLocations.RemoveAt(0);
        }

        return new IfStatement(
            _syntaxTree, keyword, openParenthesis, condition, closeParenthesis, statement, elseClause);
    }

    private ElseClause ParseElseClause() {
        if (current.type != SyntaxType.ElseKeyword)
            return null;

        var keyword = Match(SyntaxType.ElseKeyword);
        var statement = ParseStatement(true);
        return new ElseClause(_syntaxTree, keyword, statement);
    }

    private bool PeekIsInlineFunctionExpression() {
        var offset = 1;
        var stack = 1;

        if (current.type != SyntaxType.OpenBraceToken)
            return false;

        while (Peek(offset).type != SyntaxType.EndOfFileToken && stack > 0) {
            if (Peek(offset).type == SyntaxType.ReturnKeyword)
                return true;
            else if (Peek(offset).type == SyntaxType.OpenBraceToken)
                stack++;
            else if (Peek(offset).type == SyntaxType.CloseBraceToken)
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

        if (node.type == SyntaxType.InlineFunction) {
            return (Expression)node;
        } else {
            diagnostics.Push(Error.MissingReturnStatement(((BlockStatement)node).closeBrace.location));
            return null;
        }
    }

    private Node ParseBlockStatementOrInlineFunctionExpression(bool isBlock = false) {
        var statements = ImmutableArray.CreateBuilder<Statement>();
        var openBrace = Match(SyntaxType.OpenBraceToken);
        var startToken = current;

        while (current.type != SyntaxType.EndOfFileToken && current.type != SyntaxType.CloseBraceToken) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (current == startToken)
                Next();

            startToken = current;
        }

        var closeBrace = Match(SyntaxType.CloseBraceToken);

        if (isBlock)
            return new BlockStatement(_syntaxTree, openBrace, statements.ToImmutable(), closeBrace);
        else
            return new InlineFunctionExpression(_syntaxTree, openBrace, statements.ToImmutable(), closeBrace);
    }

    private Statement ParseExpressionStatement() {
        int previousCount = diagnostics.count;
        var expression = ParseExpression();
        bool popLast = previousCount != diagnostics.count;
        previousCount = diagnostics.count;
        var semicolon = Match(SyntaxType.SemicolonToken);
        popLast = popLast && previousCount != diagnostics.count;

        if (popLast)
            diagnostics.PopBack();

        return new ExpressionStatement(_syntaxTree, expression, semicolon);
    }

    private Expression ParseAssignmentExpression() {
        if (current.type == SyntaxType.IdentifierToken) {
            switch (Peek(1).type) {
                case SyntaxType.PlusEqualsToken:
                case SyntaxType.MinusEqualsToken:
                case SyntaxType.AsteriskEqualsToken:
                case SyntaxType.SlashEqualsToken:
                case SyntaxType.AmpersandEqualsToken:
                case SyntaxType.PipeEqualsToken:
                case SyntaxType.AsteriskAsteriskEqualsToken:
                case SyntaxType.CaretEqualsToken:
                case SyntaxType.LessThanLessThanEqualsToken:
                case SyntaxType.GreaterThanGreaterThanEqualsToken:
                case SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken:
                case SyntaxType.PercentEqualsToken:
                case SyntaxType.QuestionQuestionEqualsToken:
                case SyntaxType.EqualsToken:
                    var identifierToken = Next();
                    var operatorToken = Next();
                    var right = ParseAssignmentExpression();
                    return new AssignmentExpression(_syntaxTree, identifierToken, operatorToken, right);
                default:
                    break;
            }
        }

        return ParseBinaryExpression();
    }

    private Expression ParseExpression() {
        if (current.type == SyntaxType.SemicolonToken)
            return ParseEmptyExpression();

        return ParseAssignmentExpression();
    }

    private Expression ParseEmptyExpression() {
        return new EmptyExpression(_syntaxTree);
    }

    private Expression ParseBinaryExpression(int parentPrecedence = 0) {
        Expression left;
        var unaryPrecedence = current.type.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
            var op = Next();

            if (op.type == SyntaxType.PlusPlusToken || op.type == SyntaxType.MinusMinusToken) {
                var operand = Match(SyntaxType.IdentifierToken);
                left = new PrefixExpression(_syntaxTree, op, operand);
            } else {
                var operand = ParseBinaryExpression(unaryPrecedence);
                left = new UnaryExpression(_syntaxTree, op, operand);
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
            left = new BinaryExpression(_syntaxTree, left, op, right);
        }

        return left;
    }

    private Expression ParsePrimaryExpression() {
        switch (current.type) {
            case SyntaxType.OpenParenToken:
                if (PeekIsTypeClause(1, out _, out _))
                    return ParseCastExpression();
                else
                    return ParseParenthesizedExpression();
            case SyntaxType.TrueKeyword:
            case SyntaxType.FalseKeyword:
                return ParseBooleanLiteral();
            case SyntaxType.NumericLiteralToken:
                return ParseNumericLiteral();
            case SyntaxType.StringLiteralToken:
                return ParseStringLiteral();
            case SyntaxType.NullKeyword:
                return ParseNullLiteral();
            case SyntaxType.OpenBraceToken:
                if (PeekIsInlineFunctionExpression())
                    return ParseInlineFunctionExpression();
                else
                    return ParseInitializerListExpression();
            case SyntaxType.RefKeyword:
                return ParseReferenceExpression();
            case SyntaxType.IdentifierToken:
                if (Peek(1).type == SyntaxType.PlusPlusToken || Peek(1).type == SyntaxType.MinusMinusToken)
                    return ParsePostfixExpression();
                else
                    goto default;
            case SyntaxType.NameExpression:
            case SyntaxType.TypeOfKeyword:
            default:
                return ParseNameOrPrimaryOperatorExpression();
        }
    }

    private Expression ParseCastExpression() {
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var typeClause = ParseTypeClause();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);
        var expression = ParseExpression();

        return new CastExpression(_syntaxTree, openParenthesis, typeClause, closeParenthesis, expression);
    }

    private Expression ParseReferenceExpression() {
        var refKeyword = Match(SyntaxType.RefKeyword);
        var identifier = Match(SyntaxType.IdentifierToken);

        return new ReferenceExpression(_syntaxTree, refKeyword, identifier);
    }

    private Expression ParsePostfixExpression() {
        var operand = Match(SyntaxType.IdentifierToken);
        Token op = null;

        if (current.type == SyntaxType.MinusMinusToken || current.type == SyntaxType.PlusPlusToken)
            op = Next();

        return new PostfixExpression(_syntaxTree, operand, op);
    }

    private Expression ParseInitializerListExpression() {
        var left = Match(SyntaxType.OpenBraceToken);
        var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

        var parseNextItem = true;
        while (parseNextItem &&
            current.type != SyntaxType.CloseBraceToken &&
            current.type != SyntaxType.EndOfFileToken) {
            var expression = ParseExpression();
            nodesAndSeparators.Add(expression);

            if (current.type == SyntaxType.CommaToken) {
                var comma = Next();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
        var right = Match(SyntaxType.CloseBraceToken);
        return new InitializerListExpression(_syntaxTree, left, separatedSyntaxList, right);
    }

    private Expression ParseNullLiteral() {
        var token = Match(SyntaxType.NullKeyword);
        return new LiteralExpression(_syntaxTree, token);
    }

    private Expression ParseNumericLiteral() {
        var token = Match(SyntaxType.NumericLiteralToken);
        return new LiteralExpression(_syntaxTree, token);
    }

    private Expression ParseParenthesizedExpression() {
        var left = Match(SyntaxType.OpenParenToken);
        var expression = ParseExpression();
        var right = Match(SyntaxType.CloseParenToken);
        return new ParenthesisExpression(_syntaxTree, left, expression, right);
    }

    private Expression ParseBooleanLiteral() {
        var isTrue = current.type == SyntaxType.TrueKeyword;
        var keyword = isTrue ? Match(SyntaxType.TrueKeyword) : Match(SyntaxType.FalseKeyword);
        return new LiteralExpression(_syntaxTree, keyword, isTrue);
    }

    private Expression ParseStringLiteral() {
        var stringToken = Match(SyntaxType.StringLiteralToken);
        return new LiteralExpression(_syntaxTree, stringToken);
    }

    private Expression ParsePrimaryOperatorExpression(
        Node operand, int parentPrecedence = 0, Token maybeUnexpected=null) {
        Node ParseCorrectPrimaryOperator(Node operand) {
            if (operand.type == SyntaxType.TypeOfKeyword)
                return ParseTypeOfExpression();
            else if (current.type == SyntaxType.OpenParenToken)
                return ParseCallExpression((Expression)operand);
            else if (current.type == SyntaxType.OpenBracketToken)
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

    private Expression ParseTypeOfExpression() {
        var typeofKeyword = Next();
        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var typeClause = ParseTypeClause(false);
        var closeParenthesis = Match(SyntaxType.CloseParenToken);

        return new TypeOfExpression(_syntaxTree, typeofKeyword, openParenthesis, typeClause, closeParenthesis);
    }

    private Expression ParseNameOrPrimaryOperatorExpression() {
        var maybeUnexpected = current;

        Node left;
        if (current.type == SyntaxType.TypeOfKeyword)
            left = current;
        else
            left = ParseNameExpression(true);

        return ParsePrimaryOperatorExpression(left, maybeUnexpected: maybeUnexpected);
    }

    private Expression ParseIndexExpression(Expression operand) {
        var openBracket = Match(SyntaxType.OpenBracketToken);
        var index = ParseExpression();
        var closeBracket = Match(SyntaxType.CloseBracketToken);

        return new IndexExpression(_syntaxTree, operand, openBracket, index, closeBracket);
    }

    private Expression ParseCallExpression(Expression operand) {
        if (operand.type != SyntaxType.NameExpression) {
            diagnostics.Push(Error.ExpectedMethodName(operand.location));
            return operand;
        }

        var openParenthesis = Match(SyntaxType.OpenParenToken);
        var arguments = ParseArguments();
        var closeParenthesis = Match(SyntaxType.CloseParenToken);

        return new CallExpression(_syntaxTree, (NameExpression)operand, openParenthesis, arguments, closeParenthesis);
    }

    private SeparatedSyntaxList<Expression> ParseArguments() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<Node>();

        var parseNextArgument = true;
        while (parseNextArgument &&
            current.type != SyntaxType.CloseParenToken &&
            current.type != SyntaxType.EndOfFileToken) {
            if (current.type != SyntaxType.CommaToken) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            }

            if (current.type == SyntaxType.CommaToken) {
                var comma = Next();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextArgument = false;
            }
        }

        return new SeparatedSyntaxList<Expression>(nodesAndSeparators.ToImmutable());
    }

    private Expression ParseNameExpression(bool suppressErrors = false) {
        var identifier = Match(SyntaxType.IdentifierToken, suppressErrors: suppressErrors);
        return new NameExpression(_syntaxTree, identifier);
    }
}
