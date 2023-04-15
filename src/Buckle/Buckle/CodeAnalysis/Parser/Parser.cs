using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Lexes then parses text into a tree of SyntaxNodes, in doing so doing syntax checking.
/// </summary>
internal sealed class Parser {
    private static readonly ObjectPool<BlendedNode[]> _blendedNodesPool =
        new ObjectPool<BlendedNode[]>(() => new BlendedNode[32], 2);

    private readonly Lexer _lexer;
    private readonly SourceText _text;
    private readonly bool _isIncremental;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly SyntaxFactory _syntaxFactory;
    private readonly SyntaxTree _syntaxTree;

    private bool _expectParenthesis;

    // Treat all of these as readonly unless you know exactly what you are doing
    private Blender _firstBlender;
    private ArrayElement<SyntaxToken>[] _lexedTokens;
    private BlendedNode[] _blendedTokens;
    private BlendedNode _currentNode;
    private SyntaxToken _currentToken;
    private int _tokenOffset;
    private int _tokenCount;

    /// <summary>
    /// Creates a new <see cref="Parser" />, requiring a fully initialized <see cref="SyntaxTree" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to parse from.</param>
    internal Parser(SyntaxTree syntaxTree) : this(syntaxTree, null, null) { }

    /// <summary>
    /// Creates a new <see cref="Parser" />, requiring a fully initialized <see cref="SyntaxTree" />.
    /// In addition, incremental parsing is enabled by passing in the previous tree and all changes.
    /// </summary>
    internal Parser(SyntaxTree syntaxTree, SyntaxNode oldTree, ImmutableArray<TextChangeRange>? changes) {
        _diagnostics = new BelteDiagnosticQueue();
        _text = syntaxTree.text;
        _syntaxTree = syntaxTree;
        _lexer = new Lexer(_syntaxTree);
        _expectParenthesis = false;
        _isIncremental = oldTree != null && changes.HasValue && changes.Value.Length > 0;
        _syntaxFactory = new SyntaxFactory(_syntaxTree);

        if (_isIncremental) {
            _firstBlender = new Blender(_lexer, oldTree, changes.Value);
            _blendedTokens = _blendedNodesPool.Allocate();
        } else {
            _firstBlender = null;
            PreLex();
        }
    }

    /// <summary>
    /// Diagnostics produced during the parsing process.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics {
        get {
            if (_lexer.diagnostics.Any())
                _diagnostics.Move(_lexer.diagnostics);

            return _diagnostics;
        }
    }

    private SyntaxToken currentToken {
        get {
            return _currentToken ?? (_currentToken = FetchCurrentToken());
        }
    }

    // Used for reusing nodes from the old tree. Just validate and call EatNode to reuse an old node.
    private SyntaxNode currentNode {
        get {
            var node = _currentNode.node;

            if (node != null)
                return node;

            ReadCurrentNode();
            return _currentNode.node;
        }
    }

    /// <summary>
    /// Parses the entirety of a single file.
    /// </summary>
    /// <returns>The parsed file.</returns>
    internal CompilationUnitSyntax ParseCompilationUnit() {
        var members = ParseMembers(true);
        var endOfFile = Match(SyntaxKind.EndOfFileToken);
        return _syntaxFactory.CompilationUnit(members, endOfFile);
    }

    private void PreLex() {
        var size = Math.Min(4096, Math.Max(32, _syntaxTree.text.length / 2));
        _lexedTokens = new ArrayElement<SyntaxToken>[size];

        for (var i = 0; i < size; i++) {
            var token = _lexer.LexNext();

            AddLexedToken(token);

            if (token.kind == SyntaxKind.EndOfFileToken)
                break;
        }
    }

    private SyntaxNode EatNode() {
        var saved = currentNode;

        if (_tokenOffset >= _blendedTokens.Length)
            AddTokenSlot();

        _blendedTokens[_tokenOffset++] = _currentNode;
        _tokenCount = _tokenOffset;

        _currentNode = null;
        _currentToken = null;

        return saved;
    }

    private SyntaxToken EatToken() {
        var saved = currentToken;
        MoveToNextToken();
        return saved;
    }

    private void ReadCurrentNode() {
        if (_tokenOffset == 0)
            _currentNode = _firstBlender.ReadNode();
        else
            _currentNode = _blendedTokens[_tokenOffset - 1].blender.ReadNode();
    }

    private SyntaxToken FetchCurrentToken() {
        if (_tokenOffset >= _tokenCount)
            AddNewToken();

        if (_blendedTokens != null)
            return _blendedTokens[_tokenOffset].token;
        else
            return _lexedTokens[_tokenOffset];
    }

    private void AddLexedToken(SyntaxToken token) {
        if (_tokenCount >= _lexedTokens.Length) {
            var temp = new ArrayElement<SyntaxToken>[_lexedTokens.Length * 2];
            Array.Copy(_lexedTokens, temp, _lexedTokens.Length);
            _lexedTokens = temp;
        }

        _lexedTokens[_tokenCount].Value = token;
        _tokenCount++;
    }

    private void AddNewToken() {
        if (_blendedTokens != null) {
            if (_tokenCount > 0) {
                AddToken(_blendedTokens[_tokenCount - 1].blender.ReadToken());
            } else {
                if (_currentNode?.token != null)
                    AddToken(_currentNode);
                else
                    AddToken(_firstBlender.ReadToken());
            }
        } else {
            AddLexedToken(_lexer.LexNext());
        }
    }

    private void AddToken(in BlendedNode token) {
        if (_tokenCount >= _blendedTokens.Length)
            AddTokenSlot();

        _blendedTokens[_tokenCount] = token;
        _tokenCount++;
    }

    private void AddTokenSlot() {
        var old = _blendedTokens;
        Array.Resize(ref _blendedTokens, _blendedTokens.Length * 2);
        _blendedNodesPool.ForgetTrackedObject(old, replacement: _blendedTokens);
    }

    private SyntaxToken Match(SyntaxKind kind, SyntaxKind? nextWanted = null) {
        if (nextWanted == null && _expectParenthesis)
            nextWanted = SyntaxKind.CloseParenToken;

        if (currentToken.kind == kind)
            return EatToken();

        if (nextWanted != null && currentToken.kind == nextWanted) {
            diagnostics.Push(Error.ExpectedToken(currentToken.location, kind));

            return _syntaxFactory.Token(kind, currentToken.position);
        }

        if (Peek(1).kind != kind) {
            diagnostics.Push(Error.UnexpectedToken(currentToken.location, currentToken.kind, kind));
            var skipped = currentToken;
            EatToken();

            return _syntaxFactory.Token(kind, skipped.position);
        }

        diagnostics.Push(Error.UnexpectedToken(currentToken.location, currentToken.kind));
        EatToken();

        return EatToken();
    }

    private void MoveToNextToken() {
        _currentToken = null;

        if (_blendedTokens != null)
            _currentNode = null;

        _tokenOffset++;
    }

    private SyntaxToken Peek(int offset) {
        var index = _tokenOffset + offset;

        while (index >= _tokenCount)
            AddNewToken();

        if (index < 0)
            index = 0;

        if (_blendedTokens != null)
            return _blendedTokens[index].token;
        else
            return _lexedTokens[index];
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
                        if (Peek(offset + 1).kind == SyntaxKind.OpenBraceToken)
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
                Peek(finalOffset).kind == SyntaxKind.RefKeyword) {
                finalOffset++;
            }

            if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken ||
                Peek(finalOffset).kind == SyntaxKind.VarKeyword ||
                Peek(finalOffset - 1).kind == SyntaxKind.ConstKeyword) {
                if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken ||
                    Peek(finalOffset).kind == SyntaxKind.VarKeyword) {
                    finalOffset++;
                }

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
        if (currentToken.kind == SyntaxKind.OpenParenToken &&
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

    private ImmutableArray<MemberSyntax> ParseMembers(bool isGlobal = false) {
        var members = ImmutableArray.CreateBuilder<MemberSyntax>();

        while (currentToken.kind != SyntaxKind.EndOfFileToken) {
            if (!isGlobal && currentToken.kind == SyntaxKind.CloseBraceToken)
                break;

            var startToken = currentToken;

            var member = ParseMember(isGlobal);
            members.Add(member);

            if (currentToken == startToken)
                EatToken();
        }

        return members.ToImmutable();
    }

    private bool TryParseMember(bool allowGlobalStatements, out MemberSyntax member) {
        if (currentToken.kind == SyntaxKind.BadToken) {
            member = null;
            return false;
        }

        member = ParseMember(allowGlobalStatements);
        return true;
    }

    private MemberSyntax ParseMember(bool allowGlobalStatements = false) {
        if (PeekIsFunctionOrMethodDeclaration())
            return ParseMethodDeclaration();

        switch (currentToken.kind) {
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
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var members = ParseFieldList();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return _syntaxFactory.StructDeclaration(keyword, identifier, openBrace, members, closeBrace);
    }

    private MemberSyntax ParseClassDeclaration() {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var members = new SyntaxList<MemberSyntax>(ParseMembers());
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return _syntaxFactory.ClassDeclaration(keyword, identifier, openBrace, members, closeBrace);
    }

    private MemberSyntax ParseMethodDeclaration() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return _syntaxFactory.MethodDeclaration(type, identifier, openParenthesis, parameters, closeParenthesis, body);
    }

    private StatementSyntax ParseLocalFunctionDeclaration() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameterList();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return _syntaxFactory.LocalFunctionStatement(
            type, identifier, openParenthesis, parameters, closeParenthesis, body
        );
    }

    private SeparatedSyntaxList<ParameterSyntax> ParseParameterList() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextParameter = true;

        while (parseNextParameter &&
            currentToken.kind != SyntaxKind.CloseParenToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var expression = ParseParameter();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
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

        if (currentToken.kind == SyntaxKind.EqualsToken) {
            equals = EatToken();
            defaultValue = ParseNonAssignmentExpression();
        }

        return _syntaxFactory.Parameter(type, identifier, equals, defaultValue);
    }

    private SyntaxList<MemberSyntax> ParseFieldList() {
        var fieldDeclarations = ImmutableArray.CreateBuilder<MemberSyntax>();

        while (currentToken.kind != SyntaxKind.CloseBraceToken && currentToken.kind != SyntaxKind.EndOfFileToken) {
            var field = ParseFieldDeclaration();
            fieldDeclarations.Add(field);
        }

        return new SyntaxList<MemberSyntax>(fieldDeclarations.ToImmutable());
    }

    private FieldDeclarationSyntax ParseFieldDeclaration() {
        var declaration = (VariableDeclarationStatementSyntax)ParseVariableDeclarationStatement(true);
        return _syntaxFactory.FieldDeclaration(declaration);
    }

    private MemberSyntax ParseGlobalStatement() {
        var statement = ParseStatement();
        return _syntaxFactory.GlobalStatement(statement);
    }

    private StatementSyntax ParseStatement() {
        if (PeekIsFunctionOrMethodDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsType(0, out _, out var hasName) && hasName)
            return ParseVariableDeclarationStatement();

        switch (currentToken.kind) {
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
        var keyword = EatToken();
        var body = ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause == null && finallyClause == null)
            diagnostics.Push(Error.NoCatchOrFinally(((BlockStatementSyntax)body).closeBrace.location));

        return _syntaxFactory.TryStatement(keyword, (BlockStatementSyntax)body, catchClause, finallyClause);
    }

    private CatchClauseSyntax ParseCatchClause() {
        if (currentToken.kind != SyntaxKind.CatchKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return _syntaxFactory.CatchClause(keyword, (BlockStatementSyntax)body);
    }

    private FinallyClauseSyntax ParseFinallyClause() {
        if (currentToken.kind != SyntaxKind.FinallyKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return _syntaxFactory.FinallyClause(keyword, (BlockStatementSyntax)body);
    }

    private StatementSyntax ParseReturnStatement() {
        var keyword = EatToken();
        ExpressionSyntax expression = null;

        if (currentToken.kind != SyntaxKind.SemicolonToken)
            expression = ParseExpression();

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return _syntaxFactory.ReturnStatement(keyword, expression, semicolon);
    }

    private StatementSyntax ParseContinueStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return _syntaxFactory.ContinueStatement(keyword, semicolon);
    }

    private StatementSyntax ParseBreakStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return _syntaxFactory.BreakStatement(keyword, semicolon);
    }

    private StatementSyntax ParseDoWhileStatement() {
        var doKeyword = EatToken();
        var body = ParseStatement();
        var whileKeyword = Match(SyntaxKind.WhileKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return _syntaxFactory.DoWhileStatement(
            doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon
        );
    }

    private StatementSyntax ParseVariableDeclarationStatement(bool declarationOnly = false) {
        var type = ParseType(allowImplicit: !declarationOnly, declarationOnly: declarationOnly);
        var identifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken equals = null;
        ExpressionSyntax initializer = null;

        if (currentToken.kind == SyntaxKind.EqualsToken) {
            equals = EatToken();
            initializer = ParseExpression();

            if (declarationOnly)
                diagnostics.Push(Error.Unsupported.CannotInitialize(equals.location));
        }

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return _syntaxFactory.VariableDeclarationStatement(type, identifier, equals, initializer, semicolon);
    }

    private StatementSyntax ParseWhileStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return _syntaxFactory.WhileStatement(keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private StatementSyntax ParseForStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var initializer = ParseStatement();
        var condition = ParseNonAssignmentExpression();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        ExpressionSyntax step;
        if (currentToken.kind == SyntaxKind.CloseParenToken)
            step = _syntaxFactory.Empty();
        else
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return _syntaxFactory.ForStatement(
            keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body
        );
    }

    private StatementSyntax ParseIfStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var statement = ParseStatement();

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        var nestedIf = false;
        var invalidElseLocations = new List<TextLocation>();
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

        return _syntaxFactory.IfStatement(keyword, openParenthesis, condition, closeParenthesis, statement, elseClause);
    }

    private ElseClauseSyntax ParseElseClause() {
        if (currentToken.kind != SyntaxKind.ElseKeyword)
            return null;

        var keyword = Match(SyntaxKind.ElseKeyword);
        var statement = ParseStatement();

        return _syntaxFactory.ElseClause(keyword, statement);
    }

    private StatementSyntax ParseExpressionStatement() {
        var previousCount = diagnostics.count;
        var expression = ParseExpression(allowEmpty: true);
        var popLast = previousCount != diagnostics.count;
        previousCount = diagnostics.count;
        var semicolon = Match(SyntaxKind.SemicolonToken);
        popLast = popLast && previousCount != diagnostics.count;

        if (popLast)
            diagnostics.PopBack();

        return _syntaxFactory.ExpressionStatement(expression, semicolon);
    }

    private StatementSyntax ParseBlockStatement() {
        var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var startToken = currentToken;

        while (currentToken.kind != SyntaxKind.EndOfFileToken && currentToken.kind != SyntaxKind.CloseBraceToken) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return _syntaxFactory.BlockStatement(openBrace, statements.ToImmutable(), closeBrace);
    }

    private ExpressionSyntax ParseAssignmentExpression() {
        var left = ParseOperatorExpression();

        switch (currentToken.kind) {
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
                var operatorToken = EatToken();
                var right = ParseAssignmentExpression();
                left = _syntaxFactory.AssignmentExpression(left, operatorToken, right);
                break;
            default:
                break;
        }

        return left;
    }

    private ExpressionSyntax ParseNonAssignmentExpression() {
        if (currentToken.kind == SyntaxKind.SemicolonToken)
            return ParseEmptyExpression();

        _expectParenthesis = true;
        var value = ParseOperatorExpression();
        _expectParenthesis = false;
        return value;
    }

    private ExpressionSyntax ParseExpression(bool allowEmpty = false) {
        if (currentToken.kind == SyntaxKind.SemicolonToken) {
            if (!allowEmpty)
                diagnostics.Push(Error.ExpectedToken(currentToken.location, SyntaxKind.NameExpression));

            return ParseEmptyExpression();
        }

        return ParseAssignmentExpression();
    }

    private ExpressionSyntax ParseEmptyExpression() {
        return _syntaxFactory.Empty();
    }

    private ExpressionSyntax ParseOperatorExpression(int parentPrecedence = 0) {
        ExpressionSyntax left;
        var unaryPrecedence = currentToken.kind.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence) {
            var op = EatToken();

            if (op.kind == SyntaxKind.PlusPlusToken || op.kind == SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression();
                left = _syntaxFactory.PrefixExpression(op, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = _syntaxFactory.UnaryExpression(op, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            var precedence = currentToken.kind.GetBinaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            var op = EatToken();
            var right = ParseOperatorExpression(precedence);
            left = _syntaxFactory.BinaryExpression(left, op, right);
        }

        while (true) {
            var precedence = currentToken.kind.GetTernaryPrecedence();

            if (precedence == 0 || precedence < parentPrecedence)
                break;

            var leftOp = EatToken();
            var center = ParseOperatorExpression(precedence);
            var rightOp = Match(leftOp.kind.GetTernaryOperatorPair());
            var right = ParseOperatorExpression(precedence);
            left = _syntaxFactory.TernaryExpression(left, leftOp, center, rightOp, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal() {
        switch (currentToken.kind) {
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
            if (currentToken.kind == SyntaxKind.OpenParenToken)
                return ParseCallExpression(operand);
            else if (currentToken.kind == SyntaxKind.OpenBracketToken || currentToken.kind == SyntaxKind.QuestionOpenBracketToken)
                return ParseIndexExpression(operand);
            else if (currentToken.kind == SyntaxKind.PeriodToken || currentToken.kind == SyntaxKind.QuestionPeriodToken)
                return ParseMemberAccessExpression(operand);
            else if (currentToken.kind == SyntaxKind.MinusMinusToken ||
                currentToken.kind == SyntaxKind.PlusPlusToken || currentToken.kind == SyntaxKind.ExclamationToken) {
                return ParsePostfixExpression(operand);
            }

            return operand;
        }

        left = left ?? ParsePrimaryExpressionInternal();

        while (true) {
            var startToken = currentToken;
            var precedence = currentToken.kind.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            left = ParseCorrectPrimaryOperator(left);
            left = ParsePrimaryExpression(precedence, left);

            if (startToken == currentToken)
                EatToken();
        }

        return left;
    }

    private ExpressionSyntax ParseCastExpression() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false, false, true);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();

        return _syntaxFactory.CastExpression(openParenthesis, type, closeParenthesis, expression);
    }

    private ExpressionSyntax ParseReferenceExpression() {
        var keyword = Match(SyntaxKind.RefKeyword);
        var identifier = Match(SyntaxKind.IdentifierToken);

        return _syntaxFactory.ReferenceExpression(keyword, identifier);
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax operand) {
        var op = EatToken();
        return _syntaxFactory.PostfixExpression(operand, op);
    }

    private ExpressionSyntax ParseInitializerListExpression() {
        var left = Match(SyntaxKind.OpenBraceToken);
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextItem = true;

        while (parseNextItem &&
            currentToken.kind != SyntaxKind.EndOfFileToken &&
            currentToken.kind != SyntaxKind.CloseBraceToken) {
            if (currentToken.kind != SyntaxKind.CommaToken && currentToken.kind != SyntaxKind.CloseBraceToken) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            } else {
                var empty = _syntaxFactory.Empty(_syntaxFactory.Token(SyntaxKind.BadToken, currentToken.position));
                nodesAndSeparators.Add(empty);
            }

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToImmutable());
        var right = Match(SyntaxKind.CloseBraceToken);

        return _syntaxFactory.InitializerListExpression(left, separatedSyntaxList, right);
    }

    private ExpressionSyntax ParseParenthesizedExpression() {
        var left = Match(SyntaxKind.OpenParenToken);
        var expression = ParseExpression();
        var right = Match(SyntaxKind.CloseParenToken);

        return _syntaxFactory.ParenthesisExpression(left, expression, right);
    }

    private ExpressionSyntax ParseTypeOfExpression() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return _syntaxFactory.TypeOfExpression(keyword, openParenthesis, type, closeParenthesis);
    }

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax operand) {
        var op = EatToken();
        var member = Match(SyntaxKind.IdentifierToken);

        return _syntaxFactory.MemberAccessExpression(operand, op, member);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax operand) {
        var openBracket = EatToken();
        var index = ParseExpression();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return _syntaxFactory.IndexExpression(operand, openBracket, index, closeBracket);
    }

    private ExpressionSyntax ParseNameExpression() {
        var identifier = _syntaxFactory.Token(SyntaxKind.IdentifierToken, currentToken.position);

        if (currentToken.kind == SyntaxKind.IdentifierToken)
            identifier = EatToken();
        else
            diagnostics.Push(Error.ExpectedToken(currentToken.location, "expression"));

        return _syntaxFactory.NameExpression(identifier);
    }

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax operand) {
        if (operand.kind != SyntaxKind.NameExpression) {
            diagnostics.Push(Error.ExpectedMethodName(operand.location));
            return operand;
        }

        var openParenthesis = EatToken();
        var arguments = ParseArguments();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return _syntaxFactory.CallExpression(
            (NameExpressionSyntax)operand, openParenthesis, arguments, closeParenthesis
        );
    }

    private SeparatedSyntaxList<ArgumentSyntax> ParseArguments() {
        var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();
        var parseNextArgument = true;

        if (currentToken.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && currentToken.kind != SyntaxKind.EndOfFileToken) {
                if (currentToken.kind != SyntaxKind.CommaToken && currentToken.kind != SyntaxKind.CloseParenToken) {
                    var argument = ParseArgument();
                    nodesAndSeparators.Add(argument);
                } else {
                    var empty = _syntaxFactory.Argument(
                        null,
                        null,
                        _syntaxFactory.Empty(_syntaxFactory.Token(SyntaxKind.BadToken, currentToken.position))
                    );

                    nodesAndSeparators.Add(empty);
                }

                if (currentToken.kind == SyntaxKind.CommaToken) {
                    var comma = EatToken();
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

        if (currentToken.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.ColonToken) {
            name = EatToken();
            colon = Match(SyntaxKind.ColonToken);
        }

        ExpressionSyntax expression;
        if (currentToken.kind == SyntaxKind.CommaToken || currentToken.kind == SyntaxKind.CloseParenToken)
            expression = _syntaxFactory.Empty();
        else
            expression = ParseNonAssignmentExpression();

        return _syntaxFactory.Argument(name, colon, expression);
    }

    private SyntaxList<AttributeSyntax> ParseAttributes() {
        var attributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

        while (currentToken.kind == SyntaxKind.OpenBracketToken)
            attributes.Add(ParseAttribute());

        return new SyntaxList<AttributeSyntax>(attributes.ToImmutable());
    }

    private AttributeSyntax ParseAttribute() {
        var openBracket = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return _syntaxFactory.Attribute(openBracket, identifier, closeBracket);
    }

    private TypeSyntax ParseType(bool allowImplicit = true, bool expectName = true, bool declarationOnly = false) {
        var attributes = ParseAttributes();

        SyntaxToken constRefKeyword = null;
        SyntaxToken refKeyword = null;
        SyntaxToken constKeyword = null;
        SyntaxToken varKeyword = null;
        SyntaxToken typeName = null;

        if (currentToken.kind == SyntaxKind.ConstKeyword && Peek(1).kind == SyntaxKind.RefKeyword)
            constRefKeyword = EatToken();

        if (currentToken.kind == SyntaxKind.RefKeyword) {
            refKeyword = EatToken();

            if (declarationOnly)
                diagnostics.Push(Error.CannotUseRef(refKeyword.location));
        }

        if (currentToken.kind == SyntaxKind.ConstKeyword) {
            constKeyword = EatToken();

            if (declarationOnly)
                diagnostics.Push(Error.CannotUseConst(constKeyword.location));
            else if (!allowImplicit &&
                (Peek(1).kind != SyntaxKind.IdentifierToken && Peek(1).kind != SyntaxKind.OpenBracketToken)) {
                diagnostics.Push(Error.CannotUseImplicit(constKeyword.location));
            }
        }

        if (currentToken.kind == SyntaxKind.VarKeyword) {
            varKeyword = EatToken();

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

        while (currentToken.kind == SyntaxKind.OpenBracketToken) {
            var openBracket = EatToken();
            var closeBracket = Match(SyntaxKind.CloseBracketToken);
            brackets.Add((openBracket, closeBracket));
        }

        return _syntaxFactory.Type(
            attributes, constRefKeyword, refKeyword, constKeyword, varKeyword, typeName, brackets.ToImmutable()
        );
    }

    private ExpressionSyntax ParseNullLiteral() {
        var token = Match(SyntaxKind.NullKeyword);
        return _syntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseNumericLiteral() {
        var token = Match(SyntaxKind.NumericLiteralToken);
        return _syntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseBooleanLiteral() {
        var isTrue = currentToken.kind == SyntaxKind.TrueKeyword;
        var keyword = isTrue ? Match(SyntaxKind.TrueKeyword) : Match(SyntaxKind.FalseKeyword);
        return _syntaxFactory.Literal(keyword, isTrue);
    }

    private ExpressionSyntax ParseStringLiteral() {
        var stringToken = Match(SyntaxKind.StringLiteralToken);
        return _syntaxFactory.Literal(stringToken);
    }
}
