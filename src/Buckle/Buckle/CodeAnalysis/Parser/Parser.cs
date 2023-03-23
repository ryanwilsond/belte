using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Syntax.SyntaxFactory;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Lexes then parses text into a tree of SyntaxNodes, in doing so doing syntax checking.
/// </summary>
internal sealed class Parser {
    private readonly ImmutableArray<SyntaxToken> _tokens;
    private readonly SourceText _text;
    private readonly SyntaxTree _syntaxTree;
    private int _position;
    private bool _expectParenthesis;

    /// <summary>
    /// Creates a new <see cref="Parser" />, requiring a fully initialized <see cref="SyntaxTree" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to parse from.</param>
    internal Parser(SyntaxTree syntaxTree) {
        diagnostics = new BelteDiagnosticQueue();
        var tokens = new List<SyntaxToken>();
        var badTokens = new List<SyntaxToken>();
        var lexer = new Lexer(syntaxTree);
        SyntaxToken token;
        _text = syntaxTree.text;
        _syntaxTree = syntaxTree;
        _expectParenthesis = false;

        do {
            token = lexer.LexNext();

            if (token.kind == SyntaxKind.BadToken) {
                badTokens.Add(token);
                continue;
            }

            if (badTokens.Count > 0) {
                var leadingTrivia = token.leadingTrivia.ToBuilder();
                var index = 0;

                foreach (var badToken in badTokens) {
                    foreach (var lt in badToken.leadingTrivia)
                        leadingTrivia.Insert(index++, lt);

                    var trivia = new SyntaxTrivia(
                        syntaxTree, SyntaxKind.SkippedTokenTrivia, badToken.position, badToken.text
                    );

                    leadingTrivia.Insert(index++, trivia);

                    foreach (var tt in badToken.trailingTrivia)
                        leadingTrivia.Insert(index++, tt);
                }

                badTokens.Clear();
                token = new SyntaxToken(token.syntaxTree, token.kind, token.position,
                    token.text, token.value, leadingTrivia.ToImmutable(), token.trailingTrivia
                );
            }

            tokens.Add(token);
        } while (token.kind != SyntaxKind.EndOfFileToken);

        _tokens = tokens.ToImmutableArray();
        diagnostics.Move(lexer.diagnostics);
    }

    /// <summary>
    /// Diagnostics produced during the parsing process.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    private SyntaxToken current => Peek(0);

    /// <summary>
    /// Parses the entirety of a single file.
    /// </summary>
    /// <returns>The parsed file.</returns>
    internal CompilationUnitSyntax ParseCompilationUnit() {
        var members = ParseMembers(true);
        var endOfFile = Match(SyntaxKind.EndOfFileToken);

        return new CompilationUnitSyntax(_syntaxTree, members, endOfFile);
    }

    private SyntaxToken Match(SyntaxKind kind, SyntaxKind? nextWanted = null) {
        if (nextWanted == null && _expectParenthesis)
            nextWanted = SyntaxKind.CloseParenToken;

        if (current.kind == kind)
            return Next();

        if (nextWanted != null && current.kind == nextWanted) {
            diagnostics.Push(Error.ExpectedToken(current.location, kind));

            return Token(_syntaxTree, kind, current.position);
        }

        if (Peek(1).kind != kind) {
            diagnostics.Push(Error.UnexpectedToken(current.location, current.kind, kind));
            SyntaxToken skipped = current;
            _position++;

            return Token(_syntaxTree, kind, skipped.position);
        }

        diagnostics.Push(Error.UnexpectedToken(current.location, current.kind));
        _position++;
        SyntaxToken saved = current;
        _position++;

        return saved;
    }

    private SyntaxToken Next() {
        SyntaxToken saved = current;
        _position++;

        return saved;
    }

    private SyntaxToken Peek(int offset) {
        var index = _position + offset;

        if (index >= _tokens.Length)
            return _tokens[_tokens.Length - 1];

        if (index < 0)
            return _tokens[0];

        return _tokens[index];
    }

    private bool PeekIsFunctionOrMethodDeclaration() {
        // TODO Rewrite this so it does not look at the entire source until an EOF
        if (PeekIsType(0, out var offset, out var hasName)) {
            if (hasName)
                offset++;

            if (Peek(offset).kind == SyntaxKind.OpenParenToken) {
                var parenthesisStack = 0;

                while (Peek(offset).kind != SyntaxKind.EndOfFileToken) {
                    if (Peek(offset).kind == SyntaxKind.OpenParenToken)
                        parenthesisStack++;
                    else if (Peek(offset).kind == SyntaxKind.CloseParenToken)
                        parenthesisStack--;

                    if (Peek(offset).kind == SyntaxKind.CloseParenToken && parenthesisStack == 0) {
                        if (Peek(offset+1).kind == SyntaxKind.OpenBraceToken)
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

    private bool PeekIsType(int offset, out int finalOffset, out bool hasName) {
        finalOffset = offset;
        hasName = false;

        if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken ||
            Peek(finalOffset).kind == SyntaxKind.ConstKeyword ||
            Peek(finalOffset).kind == SyntaxKind.RefKeyword ||
            Peek(finalOffset).kind == SyntaxKind.VarKeyword ||
            Peek(finalOffset).kind == SyntaxKind.OpenBracketToken) {
            while (Peek(finalOffset).kind == SyntaxKind.OpenBracketToken) {
                finalOffset++;

                if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken)
                    finalOffset++;

                if (Peek(finalOffset).kind == SyntaxKind.CloseBracketToken)
                    finalOffset++;
            }

            while (Peek(finalOffset).kind == SyntaxKind.ConstKeyword ||
                Peek(finalOffset).kind == SyntaxKind.RefKeyword)
                finalOffset++;

            if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken ||
                Peek(finalOffset).kind == SyntaxKind.VarKeyword ||
                Peek(finalOffset - 1).kind == SyntaxKind.ConstKeyword) {
                if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken ||
                    Peek(finalOffset).kind == SyntaxKind.VarKeyword)
                    finalOffset++;

                var hasBrackets = false;

                while (Peek(finalOffset).kind == SyntaxKind.OpenBracketToken ||
                    Peek(finalOffset).kind == SyntaxKind.CloseBracketToken) {
                    hasBrackets = true;
                    finalOffset++;
                }

                if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken)
                    hasName = true;

                if (!hasBrackets &&
                    Peek(finalOffset - 2).kind == SyntaxKind.ConstKeyword &&
                    Peek(finalOffset - 1).kind == SyntaxKind.IdentifierToken) {
                    hasName = true;
                    finalOffset--;
                }

                return true;
            }
        }

        return false;
    }

    private bool PeekIsCastExpression() {
        if (current.kind == SyntaxKind.OpenParenToken &&
            PeekIsType(1, out var offset, out _) &&
            Peek(offset).kind == SyntaxKind.CloseParenToken) {
            if (Peek(offset + 1).kind == SyntaxKind.OpenParenToken)
                return true;

            var isBinary = Peek(offset + 1).kind.GetBinaryPrecedence() > 0;
            var isUnary = Peek(offset + 1).kind.GetBinaryPrecedence() > 0;
            var isTernary = Peek(offset + 1).kind.GetTernaryPrecedence() > 0;
            var isPrimary = Peek(offset + 1).kind.GetPrimaryPrecedence() > 0;
            var isEquals = Peek(offset + 1).kind == SyntaxKind.EqualsToken;

            if (!isBinary && !isUnary && !isTernary && !isPrimary && !isEquals)
                return true;
        }

        return false;
    }

    private ImmutableArray<MemberSyntax> ParseMembers(bool allowGlobalStatements = false) {
        var members = ImmutableArray.CreateBuilder<MemberSyntax>();

        while (current.kind != SyntaxKind.EndOfFileToken) {
            var startToken = current;

            var member = ParseMember(allowGlobalStatements);
            members.Add(member);

            if (current == startToken)
                Next();
        }

        return members.ToImmutable();
    }

    private MemberSyntax ParseMember(bool allowGlobalStatements = false) {
        if (PeekIsFunctionOrMethodDeclaration())
            return ParseMethodDeclaration();

        switch (current.kind) {
            case SyntaxKind.StructKeyword:
                return ParseStructDeclaration();
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration();
            default:
                if (allowGlobalStatements)
                    return ParseGlobalStatement();
                else
                    return ParseFieldDeclaration();
        }
    }

    private MemberSyntax ParseStructDeclaration() {
        var keyword = Next();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var members = ParseFieldList();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return new StructDeclarationSyntax(_syntaxTree, keyword, identifier, openBrace, members, closeBrace);
    }

    private MemberSyntax ParseClassDeclaration() {
        var keyword = Next();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var members = new SyntaxList<MemberSyntax>(ParseMembers());
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return new ClassDeclarationSyntax(_syntaxTree, keyword, identifier, openBrace, members, closeBrace);
    }

    private MemberSyntax ParseMethodDeclaration() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return new MethodDeclarationSyntax(
            _syntaxTree, type, identifier, openParenthesis, parameters, closeParenthesis, body
        );
    }

    private StatementSyntax ParseLocalFunctionDeclaration() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return new LocalFunctionStatementSyntax(
            _syntaxTree, type, identifier, openParenthesis, parameters, closeParenthesis, body
        );
    }

    private SeparatedSyntaxList<ParameterSyntax> ParseParameterList() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextParameter = true;

        while (parseNextParameter &&
            current.kind != SyntaxKind.CloseParenToken &&
            current.kind != SyntaxKind.EndOfFileToken) {
            var expression = ParseParameter();
            nodesAndSeparators.Add(expression);

            if (current.kind == SyntaxKind.CommaToken) {
                var comma = Next();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextParameter = false;
            }
        }

        return new SeparatedSyntaxList<ParameterSyntax>(nodesAndSeparators.ToImmutable());
    }

    private ParameterSyntax ParseParameter() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken equals = null;
        ExpressionSyntax defaultValue = null;

        if (current.kind == SyntaxKind.EqualsToken) {
            equals = Next();
            defaultValue = ParseNonAssignmentExpression();
        }

        return new ParameterSyntax(_syntaxTree, type, identifier, equals, defaultValue);
    }

    private SyntaxList<MemberSyntax> ParseFieldList() {
        var fieldDeclarations = ImmutableArray.CreateBuilder<MemberSyntax>();

        while (current.kind != SyntaxKind.CloseBraceToken && current.kind != SyntaxKind.EndOfFileToken) {
            var field = ParseFieldDeclaration();
            fieldDeclarations.Add(field);
        }

        return new SyntaxList<MemberSyntax>(fieldDeclarations.ToImmutable());
    }

    private FieldDeclarationSyntax ParseFieldDeclaration() {
        var declaration = (VariableDeclarationStatementSyntax)ParseVariableDeclarationStatement(true);

        return new FieldDeclarationSyntax(_syntaxTree, declaration);
    }

    private MemberSyntax ParseGlobalStatement() {
        var statement = ParseStatement();

        return new GlobalStatementSyntax(_syntaxTree, statement);
    }

    private StatementSyntax ParseStatement() {
        if (PeekIsFunctionOrMethodDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsType(0, out _, out var hasName) && hasName)
            return ParseVariableDeclarationStatement();

        switch (current.kind) {
            case SyntaxKind.OpenBraceToken:
                return ParseBlockStatement();
            case SyntaxKind.IfKeyword:
                return ParseIfStatement();
            case SyntaxKind.WhileKeyword:
                return ParseWhileStatement();
            case SyntaxKind.ForKeyword:
                return ParseForStatement();
            case SyntaxKind.DoKeyword:
                return ParseDoWhileStatement();
            case SyntaxKind.TryKeyword:
                return ParseTryStatement();
            case SyntaxKind.BreakKeyword:
                return ParseBreakStatement();
            case SyntaxKind.ContinueKeyword:
                return ParseContinueStatement();
            case SyntaxKind.ReturnKeyword:
                return ParseReturnStatement();
            default:
                return ParseExpressionStatement();
        }
    }

    private StatementSyntax ParseTryStatement() {
        var keyword = Next();
        var body = ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause == null && finallyClause == null)
            diagnostics.Push(Error.NoCatchOrFinally(((BlockStatementSyntax)body).closeBrace.location));

        return new TryStatementSyntax(_syntaxTree, keyword, (BlockStatementSyntax)body, catchClause, finallyClause);
    }

    private CatchClauseSyntax ParseCatchClause() {
        if (current.kind != SyntaxKind.CatchKeyword)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();

        return new CatchClauseSyntax(_syntaxTree, keyword, (BlockStatementSyntax)body);
    }

    private FinallyClauseSyntax ParseFinallyClause() {
        if (current.kind != SyntaxKind.FinallyKeyword)
            return null;

        var keyword = Next();
        var body = ParseBlockStatement();

        return new FinallyClauseSyntax(_syntaxTree, keyword, (BlockStatementSyntax)body);
    }

    private StatementSyntax ParseReturnStatement() {
        var keyword = Next();
        ExpressionSyntax expression = null;

        if (current.kind != SyntaxKind.SemicolonToken)
            expression = ParseExpression();

        SyntaxToken semicolon = Match(SyntaxKind.SemicolonToken);

        return new ReturnStatementSyntax(_syntaxTree, keyword, expression, semicolon);
    }

    private StatementSyntax ParseContinueStatement() {
        var keyword = Next();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return new ContinueStatementSyntax(_syntaxTree, keyword, semicolon);
    }

    private StatementSyntax ParseBreakStatement() {
        var keyword = Next();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return new BreakStatementSyntax(_syntaxTree, keyword, semicolon);
    }

    private StatementSyntax ParseDoWhileStatement() {
        var doKeyword = Next();
        var body = ParseStatement();
        var whileKeyword = Match(SyntaxKind.WhileKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return new DoWhileStatementSyntax(
            _syntaxTree, doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon
        );
    }

    private StatementSyntax ParseVariableDeclarationStatement(bool declarationOnly = false) {
        var type = ParseType(allowImplicit: !declarationOnly, declarationOnly: declarationOnly);
        var identifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken equals = null;
        ExpressionSyntax initializer = null;

        if (current.kind == SyntaxKind.EqualsToken) {
            equals = Next();
            initializer = ParseExpression();

            if (declarationOnly)
                diagnostics.Push(Error.Unsupported.CannotInitialize(equals.location));
        }

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return new VariableDeclarationStatementSyntax(
            _syntaxTree, type, identifier, equals, initializer, semicolon
        );
    }

    private StatementSyntax ParseWhileStatement() {
        var keyword = Next();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return new WhileStatementSyntax(_syntaxTree, keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private StatementSyntax ParseForStatement() {
        var keyword = Next();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var initializer = ParseStatement();
        var condition = ParseNonAssignmentExpression();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        ExpressionSyntax step = null;

        if (current.kind == SyntaxKind.CloseParenToken)
            step = new EmptyExpressionSyntax(_syntaxTree);
        else
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return new ForStatementSyntax(
            _syntaxTree, keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body
        );
    }

    private StatementSyntax ParseIfStatement() {
        var keyword = Next();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var statement = ParseStatement();

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        bool nestedIf = false;
        List<TextLocation> invalidElseLocations = new List<TextLocation>();
        var inter = statement;

        while (inter.kind == SyntaxKind.IfStatement) {
            nestedIf = true;
            var interIf = (IfStatementSyntax)inter;

            if (interIf.elseClause != null && interIf.then.kind != SyntaxKind.BlockStatement)
                invalidElseLocations.Add(interIf.elseClause.keyword.location);

            if (interIf.then.kind == SyntaxKind.IfStatement)
                inter = interIf.then;
            else
                break;
        }

        var elseClause = ParseElseClause();

        if (elseClause != null && statement.kind != SyntaxKind.BlockStatement && nestedIf)
            invalidElseLocations.Add(elseClause.keyword.location);

        while (invalidElseLocations.Count > 0) {
            diagnostics.Push(Error.AmbiguousElse(invalidElseLocations[0]));
            invalidElseLocations.RemoveAt(0);
        }

        return new IfStatementSyntax(
            _syntaxTree, keyword, openParenthesis, condition, closeParenthesis, statement, elseClause
        );
    }

    private ElseClauseSyntax ParseElseClause() {
        if (current.kind != SyntaxKind.ElseKeyword)
            return null;

        var keyword = Match(SyntaxKind.ElseKeyword);
        var statement = ParseStatement();

        return new ElseClauseSyntax(_syntaxTree, keyword, statement);
    }

    private StatementSyntax ParseExpressionStatement() {
        int previousCount = diagnostics.count;
        var expression = ParseExpression();
        bool popLast = previousCount != diagnostics.count;
        previousCount = diagnostics.count;
        var semicolon = Match(SyntaxKind.SemicolonToken);
        popLast = popLast && previousCount != diagnostics.count;

        if (popLast)
            diagnostics.PopBack();

        return new ExpressionStatementSyntax(_syntaxTree, expression, semicolon);
    }

    private StatementSyntax ParseBlockStatement() {
        var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var startToken = current;

        while (current.kind != SyntaxKind.EndOfFileToken && current.kind != SyntaxKind.CloseBraceToken) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (current == startToken)
                Next();

            startToken = current;
        }

        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return new BlockStatementSyntax(_syntaxTree, openBrace, statements.ToImmutable(), closeBrace);
    }

    private ExpressionSyntax ParseAssignmentExpression() {
        var left = ParseOperatorExpression();

        switch (current.kind) {
            case SyntaxKind.PlusEqualsToken:
            case SyntaxKind.MinusEqualsToken:
            case SyntaxKind.AsteriskEqualsToken:
            case SyntaxKind.SlashEqualsToken:
            case SyntaxKind.AmpersandEqualsToken:
            case SyntaxKind.PipeEqualsToken:
            case SyntaxKind.AsteriskAsteriskEqualsToken:
            case SyntaxKind.CaretEqualsToken:
            case SyntaxKind.LessThanLessThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
            case SyntaxKind.PercentEqualsToken:
            case SyntaxKind.QuestionQuestionEqualsToken:
            case SyntaxKind.EqualsToken:
                var operatorToken = Next();
                var right = ParseAssignmentExpression();
                left = new AssignmentExpressionSyntax(_syntaxTree, left, operatorToken, right);
                break;
            default:
                break;
        }

        return left;
    }

    private ExpressionSyntax ParseNonAssignmentExpression() {
        if (current.kind == SyntaxKind.SemicolonToken)
            return ParseEmptyExpression();

        _expectParenthesis = true;
        var value = ParseOperatorExpression();
        _expectParenthesis = false;
        return value;
    }

    private ExpressionSyntax ParseExpression() {
        if (current.kind == SyntaxKind.SemicolonToken)
            return ParseEmptyExpression();

        return ParseAssignmentExpression();
    }

    private ExpressionSyntax ParseEmptyExpression() {
        return new EmptyExpressionSyntax(_syntaxTree);
    }

    private ExpressionSyntax ParseOperatorExpression(int parentPrecedence = 0) {
        ExpressionSyntax left;
        var unaryPrecedence = current.kind.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
            var op = Next();

            if (op.kind == SyntaxKind.PlusPlusToken || op.kind == SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression();
                left = new PrefixExpressionSyntax(_syntaxTree, op, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = new UnaryExpressionSyntax(_syntaxTree, op, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            int precedence = current.kind.GetBinaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            var op = Next();
            var right = ParseOperatorExpression(precedence);
            left = new BinaryExpressionSyntax(_syntaxTree, left, op, right);
        }

        while (true) {
            int precedence = current.kind.GetTernaryPrecedence();

            if (precedence == 0 || precedence < parentPrecedence)
                break;

            var leftOp = Next();
            var center = ParseOperatorExpression(precedence);
            var rightOp = Match(leftOp.kind.GetTernaryOperatorPair());
            var right = ParseOperatorExpression(precedence);
            left = new TernaryExpressionSyntax(_syntaxTree, left, leftOp, center, rightOp, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal() {
        switch (current.kind) {
            case SyntaxKind.OpenParenToken:
                if (PeekIsCastExpression())
                    return ParseCastExpression();
                else
                    return ParseParenthesizedExpression();
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return ParseBooleanLiteral();
            case SyntaxKind.NumericLiteralToken:
                return ParseNumericLiteral();
            case SyntaxKind.StringLiteralToken:
                return ParseStringLiteral();
            case SyntaxKind.NullKeyword:
                return ParseNullLiteral();
            case SyntaxKind.OpenBraceToken:
                return ParseInitializerListExpression();
            case SyntaxKind.RefKeyword:
                return ParseReferenceExpression();
            case SyntaxKind.TypeOfKeyword:
                return ParseTypeOfExpression();
            case SyntaxKind.IdentifierToken:
            default:
                return ParseNameExpression();
        }
    }

    private ExpressionSyntax ParsePrimaryExpression(int parentPrecedence = 0, ExpressionSyntax left = null) {
        ExpressionSyntax ParseCorrectPrimaryOperator(ExpressionSyntax operand) {
            if (current.kind == SyntaxKind.OpenParenToken)
                return ParseCallExpression(operand);
            else if (current.kind == SyntaxKind.OpenBracketToken || current.kind == SyntaxKind.QuestionOpenBracketToken)
                return ParseIndexExpression(operand);
            else if (current.kind == SyntaxKind.PeriodToken || current.kind == SyntaxKind.QuestionPeriodToken)
                return ParseMemberAccessExpression(operand);
            else if (current.kind == SyntaxKind.MinusMinusToken ||
                current.kind == SyntaxKind.PlusPlusToken || current.kind == SyntaxKind.ExclamationToken)
                return ParsePostfixExpression(operand);

            return operand;
        }

        left = left == null ? ParsePrimaryExpressionInternal() : left;

        while (true) {
            var startToken = current;
            var precedence = current.kind.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            left = ParseCorrectPrimaryOperator(left);
            left = ParsePrimaryExpression(precedence, left);

            if (startToken == current)
                Next();
        }

        return left;
    }

    private ExpressionSyntax ParseCastExpression() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false, false, true);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();

        return new CastExpressionSyntax(_syntaxTree, openParenthesis, type, closeParenthesis, expression);
    }

    private ExpressionSyntax ParseReferenceExpression() {
        var keyword = Match(SyntaxKind.RefKeyword);
        var identifier = Match(SyntaxKind.IdentifierToken);

        return new ReferenceExpressionSyntax(_syntaxTree, keyword, identifier);
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax operand) {
        var op = Next();

        return new PostfixExpressionSyntax(_syntaxTree, operand, op);
    }

    private ExpressionSyntax ParseInitializerListExpression() {
        var left = Match(SyntaxKind.OpenBraceToken);
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextItem = true;

        while (parseNextItem &&
            current.kind != SyntaxKind.EndOfFileToken &&
            current.kind != SyntaxKind.CloseBraceToken) {
            if (current.kind != SyntaxKind.CommaToken && current.kind != SyntaxKind.CloseBraceToken) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            } else {
                var empty = new EmptyExpressionSyntax(
                    _syntaxTree, Token(_syntaxTree, SyntaxKind.BadToken, current.position)
                );

                nodesAndSeparators.Add(empty);
            }

            if (current.kind == SyntaxKind.CommaToken) {
                var comma = Next();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToImmutable());
        var right = Match(SyntaxKind.CloseBraceToken);

        return new InitializerListExpressionSyntax(_syntaxTree, left, separatedSyntaxList, right);
    }

    private ExpressionSyntax ParseParenthesizedExpression() {
        var left = Match(SyntaxKind.OpenParenToken);
        var expression = ParseExpression();
        var right = Match(SyntaxKind.CloseParenToken);

        return new ParenthesisExpressionSyntax(_syntaxTree, left, expression, right);
    }

    private ExpressionSyntax ParseTypeOfExpression() {
        var keyword = Next();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return new TypeOfExpressionSyntax(_syntaxTree, keyword, openParenthesis, type, closeParenthesis);
    }

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax operand) {
        var op = Next();
        var member = Match(SyntaxKind.IdentifierToken);

        return new MemberAccessExpressionSyntax(_syntaxTree, operand, op, member);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax operand) {
        var openBracket = Next();
        var index = ParseExpression();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return new IndexExpressionSyntax(_syntaxTree, operand, openBracket, index, closeBracket);
    }

    private ExpressionSyntax ParseNameExpression() {
        SyntaxToken identifier = Token(_syntaxTree, SyntaxKind.IdentifierToken, current.position);

        if (current.kind == SyntaxKind.IdentifierToken)
            identifier = Next();
        else
            diagnostics.Push(Error.ExpectedToken(current.location, "expression"));

        return new NameExpressionSyntax(_syntaxTree, identifier);
    }

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax operand) {
        if (operand.kind != SyntaxKind.NameExpression) {
            diagnostics.Push(Error.ExpectedMethodName(operand.location));

            return operand;
        }

        var openParenthesis = Next();
        var arguments = ParseArguments();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return new CallExpressionSyntax(
            _syntaxTree, (NameExpressionSyntax)operand, openParenthesis, arguments, closeParenthesis
        );
    }

    private SeparatedSyntaxList<ArgumentSyntax> ParseArguments() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextArgument = true;

        if (current.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && current.kind != SyntaxKind.EndOfFileToken) {
                if (current.kind != SyntaxKind.CommaToken && current.kind != SyntaxKind.CloseParenToken) {
                    var argument = ParseArgument();
                    nodesAndSeparators.Add(argument);
                } else {
                    var empty = new ArgumentSyntax(
                        _syntaxTree,
                        null,
                        null,
                        new EmptyExpressionSyntax(
                            _syntaxTree, Token(_syntaxTree, SyntaxKind.BadToken, current.position)
                        )
                    );

                    nodesAndSeparators.Add(empty);
                }

                if (current.kind == SyntaxKind.CommaToken) {
                    var comma = Next();
                    nodesAndSeparators.Add(comma);
                } else {
                    parseNextArgument = false;
                }
            }
        }

        return new SeparatedSyntaxList<ArgumentSyntax>(nodesAndSeparators.ToImmutable());
    }

    private ArgumentSyntax ParseArgument() {
        SyntaxToken name = null;
        SyntaxToken colon = null;

        if (current.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.ColonToken) {
            name = Next();
            colon = Match(SyntaxKind.ColonToken);
        }

        ExpressionSyntax expression = null;

        if (current.kind == SyntaxKind.CommaToken || current.kind == SyntaxKind.CloseParenToken)
            expression = new EmptyExpressionSyntax(_syntaxTree);
        else
            expression = ParseNonAssignmentExpression();

        return new ArgumentSyntax(_syntaxTree, name, colon, expression);
    }

    private SyntaxList<AttributeSyntax> ParseAttributes() {
        var attributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

        while (current.kind == SyntaxKind.OpenBracketToken)
            attributes.Add(ParseAttribute());

        return new SyntaxList<AttributeSyntax>(attributes.ToImmutable());
    }

    private AttributeSyntax ParseAttribute() {
        var openBracket = Next();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return new AttributeSyntax(_syntaxTree, openBracket, identifier, closeBracket);
    }

    private TypeSyntax ParseType(bool allowImplicit = true, bool expectName = true, bool declarationOnly = false) {
        var attributes = ParseAttributes();

        SyntaxToken constRefKeyword = null;
        SyntaxToken refKeyword = null;
        SyntaxToken constKeyword = null;
        SyntaxToken varKeyword = null;
        SyntaxToken typeName = null;

        if (current.kind == SyntaxKind.ConstKeyword && Peek(1).kind == SyntaxKind.RefKeyword)
            constRefKeyword = Next();

        if (current.kind == SyntaxKind.RefKeyword) {
            refKeyword = Next();

            if (declarationOnly)
                diagnostics.Push(Error.CannotUseRef(refKeyword.location));
        }

        if (current.kind == SyntaxKind.ConstKeyword) {
            constKeyword = Next();

            if (declarationOnly)
                diagnostics.Push(Error.CannotUseConst(constKeyword.location));
            else if (!allowImplicit &&
                (Peek(1).kind != SyntaxKind.IdentifierToken && Peek(1).kind != SyntaxKind.OpenBracketToken))
                diagnostics.Push(Error.CannotUseImplicit(constKeyword.location));
        }

        if (current.kind == SyntaxKind.VarKeyword) {
            varKeyword = Next();

            if (!allowImplicit)
                diagnostics.Push(Error.CannotUseImplicit(varKeyword.location));
        }

        var hasTypeName = (varKeyword == null &&
            (!allowImplicit ||
                (constKeyword == null ||
                 Peek(1).kind == SyntaxKind.IdentifierToken ||
                 Peek(1).kind == SyntaxKind.OpenBracketToken
                )
            )
        );

        if (hasTypeName)
            typeName = Match(SyntaxKind.IdentifierToken);

        var brackets = ImmutableArray.CreateBuilder<(SyntaxToken openBracket, SyntaxToken closeBracket)>();

        while (current.kind == SyntaxKind.OpenBracketToken) {
            var openBracket = Next();
            var closeBracket = Match(SyntaxKind.CloseBracketToken);
            brackets.Add((openBracket, closeBracket));
        }

        return new TypeSyntax(
            _syntaxTree, attributes, constRefKeyword, refKeyword,
            constKeyword, varKeyword, typeName, brackets.ToImmutable()
        );
    }

    private ExpressionSyntax ParseNullLiteral() {
        var token = Match(SyntaxKind.NullKeyword);

        return new LiteralExpressionSyntax(_syntaxTree, token);
    }

    private ExpressionSyntax ParseNumericLiteral() {
        var token = Match(SyntaxKind.NumericLiteralToken);

        return new LiteralExpressionSyntax(_syntaxTree, token);
    }

    private ExpressionSyntax ParseBooleanLiteral() {
        var isTrue = current.kind == SyntaxKind.TrueKeyword;
        var keyword = isTrue ? Match(SyntaxKind.TrueKeyword) : Match(SyntaxKind.FalseKeyword);

        return new LiteralExpressionSyntax(_syntaxTree, keyword, isTrue);
    }

    private ExpressionSyntax ParseStringLiteral() {
        var stringToken = Match(SyntaxKind.StringLiteralToken);

        return new LiteralExpressionSyntax(_syntaxTree, stringToken);
    }
}
