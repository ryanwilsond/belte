using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Lexes and parses text into a tree of SyntaxNodes, in doing so performing syntax checking.
/// Can optionally reuse SyntaxNodes from an old tree to speed up the parsing process.
/// </summary>
internal sealed partial class Parser {
    private const int LastTerminator = (int)TerminatorState.IsEndOfTemplateArgumentList;

    private static readonly ObjectPool<BlendedNode[]> BlendedNodesPool =
        new ObjectPool<BlendedNode[]>(() => new BlendedNode[32], 2);

    private readonly Lexer _lexer;
    private readonly SourceText _text;
    private readonly bool _isIncremental;
    private readonly SyntaxTree _syntaxTree;

    private bool _expectParenthesis;

    // Treat all of these as readonly unless you know exactly what you are doing
    private Blender _firstBlender;
    private ArrayElement<SyntaxToken>[] _lexedTokens;
    private BlendedNode[] _blendedTokens;
    private BlendedNode _currentNode;
    private SyntaxToken _currentToken;
    private GreenNode _prevTokenTrailingTrivia;
    private List<Diagnostic> _futureDiagnostics;
    private int _tokenOffset;
    private int _tokenCount;
    private TerminatorState _terminatorState;
    private ParserContext _context;
    private Stack<SyntaxKind> _bracketStack;

    /// <summary>
    /// Creates a new <see cref="Parser" />, requiring a fully initialized <see cref="SyntaxTree" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to parse from.</param>
    internal Parser(SyntaxTree syntaxTree) : this(syntaxTree, null, null) { }

    /// <summary>
    /// Creates a new <see cref="Parser" />, requiring a fully initialized <see cref="SyntaxTree" />.
    /// In addition, incremental parsing is enabled by passing in the previous tree and all changes.
    /// </summary>
    internal Parser(SyntaxTree syntaxTree, SyntaxNode oldTree, IEnumerable<TextChangeRange> changes) {
        _futureDiagnostics = new List<Diagnostic>();
        _text = syntaxTree.text;
        _syntaxTree = syntaxTree;
        _lexer = new Lexer(_syntaxTree);
        _expectParenthesis = false;
        _isIncremental = oldTree != null;
        _bracketStack = new Stack<SyntaxKind>();
        _bracketStack.Push(SyntaxKind.None);

        if (_isIncremental) {
            _firstBlender = new Blender(_lexer, oldTree, changes);
            _blendedTokens = BlendedNodesPool.Allocate();
        } else {
            _firstBlender = null;
            PreLex();
        }
    }

    private SyntaxToken currentToken {
        get {
            if (_currentToken is null)
                _currentToken = FetchCurrentToken();

            return _currentToken;
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
        return SyntaxFactory.CompilationUnit(members, endOfFile);
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

    private T AddDiagnostic<T>(T node, Diagnostic diagnostic) where T : BelteSyntaxNode {
        if (!node.isFabricated) {
            return WithAdditionalDiagnostics(
                node, new SyntaxDiagnostic(diagnostic, node.GetLeadingTriviaWidth(), node.width)
            );
        }

        int offset, width;

        if (node is SyntaxToken token && token.containsSkippedText) {
            offset = token.GetLeadingTriviaWidth();
            width = 0;
            var seenSkipped = false;

            foreach (var trivia in token.trailingTrivia) {
                if (trivia.kind == SyntaxKind.SkippedTokenTrivia) {
                    seenSkipped = true;
                    width += trivia.width;
                } else if (seenSkipped) {
                    break;
                } else {
                    offset += trivia.width;
                }
            }
        } else {
            GetDiagnosticSpanForMissingToken(out offset, out width);
        }

        return WithAdditionalDiagnostics(node, new SyntaxDiagnostic(diagnostic, offset, width));
    }

    private T AddDiagnostic<T>(T node, Diagnostic diagnostic, int offset, int width) where T : BelteSyntaxNode {
        return WithAdditionalDiagnostics(node, new SyntaxDiagnostic(diagnostic, offset, width));
    }

    private T WithFutureDiagnostics<T>(T node) where T : BelteSyntaxNode {
        if (_futureDiagnostics.Count == 0)
            return node;

        var diagnostics = new SyntaxDiagnostic[_futureDiagnostics.Count];

        for (var i = 0; i < _futureDiagnostics.Count; i++)
            diagnostics[i] = new SyntaxDiagnostic(_futureDiagnostics[i], node.GetLeadingTriviaWidth(), node.width);

        _futureDiagnostics.Clear();
        return WithAdditionalDiagnostics(node, diagnostics);
    }

    private T WithAdditionalDiagnostics<T>(T node, params Diagnostic[] diagnostics) where T : BelteSyntaxNode {
        var existingDiagnostics = node.GetDiagnostics();
        var existingLength = existingDiagnostics.Length;

        if (existingLength == 0) {
            return node.WithDiagnosticsGreen(diagnostics);
        } else {
            var result = new Diagnostic[existingDiagnostics.Length + diagnostics.Length];
            existingDiagnostics.CopyTo(result, 0);
            diagnostics.CopyTo(result, existingLength);
            return node.WithDiagnosticsGreen(result);
        }
    }

    private void GetDiagnosticSpanForMissingToken(out int offset, out int width) {
        var trivia = _prevTokenTrailingTrivia;

        if (trivia != null) {
            var triviaList = new SyntaxList<BelteSyntaxNode>(trivia);
            var prevTokenHasEndOfLineTrivia = triviaList.Any(SyntaxKind.EndOfLineTrivia);

            if (prevTokenHasEndOfLineTrivia) {
                offset = -trivia.fullWidth;
                width = 0;
                return;
            }
        }

        var ct = currentToken;
        offset = ct.GetLeadingTriviaWidth();
        width = ct.width;
    }

    private T AddLeadingSkippedSyntax<T>(T node, GreenNode skippedSyntax) where T : BelteSyntaxNode {
        var oldToken = (node as SyntaxToken) ?? (SyntaxToken)node.GetFirstTerminal();
        var newToken = AddSkippedSyntax(oldToken, skippedSyntax, isTrailing: false);
        return SyntaxFirstTokenReplacer.Replace(node, newToken, skippedSyntax.fullWidth);
    }

    private SyntaxToken AddSkippedSyntax(SyntaxToken target, GreenNode skippedSyntax, bool isTrailing) {
        var builder = new SyntaxListBuilder(4);

        SyntaxDiagnostic diagnostic = null;
        var diagnosticOffset = 0;
        var currentOffset = 0;

        foreach (var node in skippedSyntax.EnumerateNodes()) {
            if (node is SyntaxToken token) {
                builder.Add(token.GetLeadingTrivia());

                if (token.width > 0) {
                    var tk = token.TokenWithLeadingTrivia(null).TokenWithTrailingTrivia(null);

                    var leadingWidth = token.GetLeadingTriviaWidth();

                    if (leadingWidth > 0) {
                        var tokenDiagnostics = tk.GetDiagnostics();

                        for (var i = 0; i < tokenDiagnostics.Length; i++) {
                            var d = (SyntaxDiagnostic)tokenDiagnostics[i];
                            tokenDiagnostics[i] = new SyntaxDiagnostic(d, d.offset - leadingWidth, d.width);
                        }
                    }

                    builder.Add(SyntaxFactory.Skipped(tk));
                } else {
                    var existing = (SyntaxDiagnostic)token.GetDiagnostics().FirstOrDefault();

                    if (existing != null) {
                        diagnostic = existing;
                        diagnosticOffset = currentOffset;
                    }
                }

                builder.Add(token.GetTrailingTrivia());
                currentOffset += token.fullWidth;
            } else if (node.containsDiagnostics && diagnostic is null) {
                var existing = (SyntaxDiagnostic)node.GetDiagnostics().FirstOrDefault();

                if (existing != null) {
                    diagnostic = existing;
                    diagnosticOffset = currentOffset;
                }
            }
        }

        var triviaWidth = currentOffset;
        var trivia = builder.ToListNode();

        int triviaOffset;

        if (isTrailing) {
            var trailingTrivia = target.GetTrailingTrivia();
            triviaOffset = target.fullWidth;
            target = target.TokenWithTrailingTrivia(SyntaxList.Concat(trailingTrivia, trivia));
        } else {
            if (triviaWidth > 0) {
                var targetDiagnostics = target.GetDiagnostics();

                for (var i = 0; i < targetDiagnostics.Length; i++) {
                    var d = (SyntaxDiagnostic)targetDiagnostics[i];
                    targetDiagnostics[i] = new SyntaxDiagnostic(d, d.offset + triviaWidth, d.width);
                }
            }

            var leadingTrivia = target.GetLeadingTrivia();
            target = target.TokenWithLeadingTrivia(SyntaxList.Concat(trivia, leadingTrivia));
            triviaOffset = 0;
        }

        if (diagnostic != null) {
            var newOffset = triviaOffset + diagnosticOffset + diagnostic.offset;
            target = WithAdditionalDiagnostics(target, new SyntaxDiagnostic(diagnostic, newOffset, diagnostic.width));
        }

        return target;
    }

    private void AddDiagnosticToNextToken(Diagnostic diagnostic) {
        _futureDiagnostics.Add(diagnostic);
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

    private SyntaxToken EatToken(bool stallDiagnostics = false) {
        var saved = currentToken;

        if (!stallDiagnostics)
            saved = WithFutureDiagnostics(saved);

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

        _lexedTokens[_tokenCount].value = token;
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
        BlendedNodesPool.ForgetTrackedObject(old, replacement: _blendedTokens);
    }

    private SyntaxToken Match(SyntaxKind kind, SyntaxKind? nextWanted = null) {
        if (nextWanted is null && _expectParenthesis)
            nextWanted = SyntaxKind.CloseParenToken;

        if (currentToken.kind == kind)
            return EatToken();

        if (nextWanted != null && currentToken.kind == nextWanted) {
            return AddDiagnostic(
                WithFutureDiagnostics(SyntaxFactory.Missing(kind)),
                Error.ExpectedToken(kind),
                currentToken.GetLeadingTriviaWidth(),
                currentToken.width
            );
        }

        if (Peek(1).kind != kind) {
            var unexpectedToken = EatToken();

            return AddDiagnostic(
                AddLeadingSkippedSyntax(SyntaxFactory.Missing(kind), unexpectedToken),
                Error.UnexpectedToken(unexpectedToken.kind, kind),
                unexpectedToken.GetLeadingTriviaWidth(),
                unexpectedToken.width
            );
        }

        var unexpected = EatToken(stallDiagnostics: true);

        return AddDiagnostic(
            WithFutureDiagnostics(AddLeadingSkippedSyntax(EatToken(), unexpected)),
            Error.UnexpectedToken(unexpected.kind),
            unexpected.GetLeadingTriviaWidth(),
            unexpected.width
        );
    }

    private void MoveToNextToken() {
        _prevTokenTrailingTrivia = _currentToken.GetTrailingTrivia();
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

    private bool PeekIsFunctionOrMethodDeclaration(bool checkForType = true, bool couldBeInStatement = true) {
        var hasName = false;
        var offset = 0;

        if (checkForType && !PeekIsType(0, out offset, out hasName, out _))
            return false;

        if (!checkForType && currentToken.kind == SyntaxKind.IdentifierToken)
            hasName = true;

        if (hasName)
            offset++;

        if (Peek(offset).kind != SyntaxKind.OpenParenToken)
            return false;

        if (!couldBeInStatement)
            return true;

        // If we get here it means that we are inside of a statement and if we do decide this is a function or method,
        // it is a local function. This logic is to make sure we don't accidentally treat a call as a function.
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

        return false;
    }

    private bool PeekIsType(int offset, out int finalOffset, out bool hasName, out bool isTemplate) {
        finalOffset = offset;
        hasName = false;
        isTemplate = false;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken and
                not SyntaxKind.ConstKeyword and
                not SyntaxKind.RefKeyword and
                not SyntaxKind.VarKeyword and
                not SyntaxKind.OpenBracketToken) {
            return false;
        }

        while (Peek(finalOffset).kind == SyntaxKind.OpenBracketToken) {
            finalOffset++;

            if (Peek(finalOffset).kind == SyntaxKind.IdentifierToken)
                finalOffset++;

            if (Peek(finalOffset).kind == SyntaxKind.CloseBracketToken)
                finalOffset++;
        }

        while (Peek(finalOffset).kind is SyntaxKind.ConstKeyword or SyntaxKind.RefKeyword)
            finalOffset++;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken and not SyntaxKind.VarKeyword &&
            Peek(finalOffset - 1).kind != SyntaxKind.ConstKeyword) {
            return false;
        }

        if (Peek(finalOffset).kind is SyntaxKind.IdentifierToken or SyntaxKind.VarKeyword)
            finalOffset++;

        while (Peek(finalOffset).kind == SyntaxKind.LessThanToken) {
            isTemplate = true;
            finalOffset++;

            while (Peek(finalOffset).kind is not SyntaxKind.GreaterThanToken and not SyntaxKind.EndOfFileToken)
                finalOffset++;

            finalOffset++;
        }

        var hasBrackets = false;

        while (Peek(finalOffset).kind is SyntaxKind.OpenBracketToken or SyntaxKind.CloseBracketToken) {
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

    private bool PeekIsCastExpression() {
        if (currentToken.kind == SyntaxKind.OpenParenToken &&
            PeekIsType(1, out var offset, out _, out _) &&
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

    private bool IsTerminator() {
        if (currentToken.kind == SyntaxKind.EndOfFileToken)
            return true;

        if (currentToken.kind == _bracketStack.Peek())
            return true;

        for (var i = 1; i < LastTerminator; i <<= 1) {
            switch (_terminatorState & (TerminatorState)i) {
                case TerminatorState.IsEndOfTemplateParameterList when IsEndOfTemplateParameterList():
                case TerminatorState.IsEndOfTemplateArgumentList when IsEndOfTemplateArgumentList():
                    return true;
            }
        }

        return false;
    }

    private bool IsEndOfTemplateParameterList() => currentToken.kind == SyntaxKind.GreaterThanToken;

    private bool IsEndOfTemplateArgumentList() => currentToken.kind == SyntaxKind.GreaterThanToken;

    private SyntaxList<MemberSyntax> ParseMembers(bool isGlobal = false) {
        var members = SyntaxListBuilder<MemberSyntax>.Create();

        while (currentToken.kind != SyntaxKind.EndOfFileToken) {
            if (!isGlobal && currentToken.kind == SyntaxKind.CloseBraceToken)
                break;

            var startToken = currentToken;

            var member = ParseMember(isGlobal);
            members.Add(member);

            if (currentToken == startToken)
                EatToken();
        }

        return members.ToList();
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
        var modifiers = ParseModifiers();

        if ((_context & ParserContext.InClassDefinition) != 0 && PeekIsFunctionOrMethodDeclaration(checkForType: false))
            return ParseConstructorDeclaration(modifiers);

        if (PeekIsFunctionOrMethodDeclaration(couldBeInStatement: allowGlobalStatements))
            return ParseMethodDeclaration(modifiers);

        switch (currentToken.kind) {
            case SyntaxKind.StructKeyword:
                return ParseStructDeclaration(modifiers);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(modifiers);
            default:
                if (allowGlobalStatements) {
                    if (modifiers.Any()) {
                        var builder = new SyntaxListBuilder<SyntaxToken>(modifiers.Count);

                        foreach (var modifier in modifiers)
                            builder.Add(AddDiagnostic(modifier, Error.UnexpectedToken(modifier.kind)));

                        modifiers = builder.ToList();
                    }

                    return ParseGlobalStatement(modifiers);
                } else {
                    return ParseFieldDeclaration(modifiers);
                }
        }
    }

    private MemberSyntax ParseStructDeclaration(SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        TemplateParameterListSyntax templateParameterList = null;

        if (currentToken.kind == SyntaxKind.LessThanToken)
            templateParameterList = ParseTemplateParameterList();

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var saved = _context;
        _context |= ParserContext.InStructDefinition;
        var members = ParseFieldList();
        _context = saved;
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.StructDeclaration(
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace
        );
    }

    private MemberSyntax ParseClassDeclaration(SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        TemplateParameterListSyntax templateParameterList = null;

        if (currentToken.kind == SyntaxKind.LessThanToken)
            templateParameterList = ParseTemplateParameterList();

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var saved = _context;
        _context |= ParserContext.InClassDefinition;
        var members = ParseMembers();
        _context = saved;
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.ClassDeclaration(
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace
        );
    }

    private ConstructorDeclarationSyntax ParseConstructorDeclaration(SyntaxList<SyntaxToken> modifiers) {
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.ConstructorDeclaration(modifiers, identifier, parameterList, body);
    }

    private MemberSyntax ParseMethodDeclaration(SyntaxList<SyntaxToken> modifiers) {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.MethodDeclaration(modifiers, type, identifier, parameterList, body);
    }

    private StatementSyntax ParseLocalFunctionDeclaration() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var parameters = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.LocalFunctionStatement(type, identifier, parameters, body);
    }

    private DeclarationModifiers GetModifier(SyntaxToken token) {
        return token.kind switch {
            SyntaxKind.StaticKeyword => DeclarationModifiers.Static,
            _ => DeclarationModifiers.None,
        };
    }

    private SyntaxList<SyntaxToken> ParseModifiers() {
        var modifiers = SyntaxListBuilder<SyntaxToken>.Create();

        while (true) {
            var modifier = GetModifier(currentToken);

            if (modifier == DeclarationModifiers.None)
                break;

            switch (modifier) {
                case DeclarationModifiers.Static:
                    modifiers.Add(EatToken());
                    break;
                default:
                    break;
            }
        }

        return modifiers.ToList();
    }

    private ParameterListSyntax ParseParameterList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameters();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ParameterList(openParenthesis, parameters, closeParenthesis);
    }

    private TemplateParameterListSyntax ParseTemplateParameterList() {
        var openAngleBracket = Match(SyntaxKind.LessThanToken);

        _bracketStack.Push(SyntaxKind.GreaterThanToken);
        var saved = _terminatorState;
        _terminatorState |= TerminatorState.IsEndOfTemplateParameterList;
        var parameters = ParseParameters();
        _terminatorState = saved;
        _bracketStack.Pop();

        var closeAngleBracket = Match(SyntaxKind.GreaterThanToken);

        return SyntaxFactory.TemplateParameterList(openAngleBracket, parameters, closeAngleBracket);
    }

    private SeparatedSyntaxList<ParameterSyntax> ParseParameters() {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
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

        return new SeparatedSyntaxList<ParameterSyntax>(nodesAndSeparators.ToList());
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

        return SyntaxFactory.Parameter(type, identifier, equals, defaultValue);
    }

    private SyntaxList<MemberSyntax> ParseFieldList() {
        var fieldDeclarations = SyntaxListBuilder<MemberSyntax>.Create();

        while (currentToken.kind is not SyntaxKind.CloseBraceToken and not SyntaxKind.EndOfFileToken) {
            var modifiers = ParseModifiers();
            var field = ParseFieldDeclaration(modifiers);
            fieldDeclarations.Add(field);
        }

        return fieldDeclarations.ToList();
    }

    private FieldDeclarationSyntax ParseFieldDeclaration(SyntaxList<SyntaxToken> modifiers) {
        var declaration = (VariableDeclarationStatementSyntax)ParseVariableDeclarationStatement(false);
        return SyntaxFactory.FieldDeclaration(modifiers, declaration);
    }

    private MemberSyntax ParseGlobalStatement(SyntaxList<SyntaxToken> modifiers) {
        var statement = ParseStatement();
        return SyntaxFactory.GlobalStatement(modifiers, statement);
    }

    private StatementSyntax ParseStatement() {
        var saved = _context;
        _context |= ParserContext.InStatement;
        var statement = ParseStatementInternal();
        _context = saved;

        return statement;
    }

    private StatementSyntax ParseStatementInternal() {
        if (PeekIsFunctionOrMethodDeclaration())
            return ParseLocalFunctionDeclaration();

        if (PeekIsType(0, out _, out var hasName, out _) && hasName)
            return ParseVariableDeclarationStatement();

        return currentToken.kind switch {
            SyntaxKind.OpenBraceToken => ParseBlockStatement(),
            SyntaxKind.IfKeyword => ParseIfStatement(),
            SyntaxKind.WhileKeyword => ParseWhileStatement(),
            SyntaxKind.ForKeyword => ParseForStatement(),
            SyntaxKind.DoKeyword => ParseDoWhileStatement(),
            SyntaxKind.TryKeyword => ParseTryStatement(),
            SyntaxKind.BreakKeyword => ParseBreakStatement(),
            SyntaxKind.ContinueKeyword => ParseContinueStatement(),
            SyntaxKind.ReturnKeyword => ParseReturnStatement(),
            _ => ParseExpressionStatement(),
        };
    }

    private StatementSyntax ParseTryStatement() {
        var keyword = EatToken();
        var body = (BlockStatementSyntax)ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause is null && finallyClause is null) {
            body = AddDiagnostic(
                body,
                Error.NoCatchOrFinally(),
                body.GetSlotOffset(2) + body.closeBrace.GetLeadingTriviaWidth(),
                body.closeBrace.width
            );
        }

        return SyntaxFactory.TryStatement(keyword, body, catchClause, finallyClause);
    }

    private CatchClauseSyntax ParseCatchClause() {
        if (currentToken.kind != SyntaxKind.CatchKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return SyntaxFactory.CatchClause(keyword, (BlockStatementSyntax)body);
    }

    private FinallyClauseSyntax ParseFinallyClause() {
        if (currentToken.kind != SyntaxKind.FinallyKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return SyntaxFactory.FinallyClause(keyword, (BlockStatementSyntax)body);
    }

    private StatementSyntax ParseReturnStatement() {
        var keyword = EatToken();
        ExpressionSyntax expression = null;

        if (currentToken.kind != SyntaxKind.SemicolonToken)
            expression = ParseExpression();

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ReturnStatement(keyword, expression, semicolon);
    }

    private StatementSyntax ParseContinueStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ContinueStatement(keyword, semicolon);
    }

    private StatementSyntax ParseBreakStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.BreakStatement(keyword, semicolon);
    }

    private StatementSyntax ParseDoWhileStatement() {
        var doKeyword = EatToken();
        var body = ParseStatement();
        var whileKeyword = Match(SyntaxKind.WhileKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.DoWhileStatement(
            doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon
        );
    }

    private StatementSyntax ParseVariableDeclarationStatement(bool allowImplicit = true) {
        var inStruct = (_context & ParserContext.InStructDefinition) != 0;
        var type = ParseType(allowImplicit: allowImplicit, declarationOnly: inStruct);
        var identifier = Match(SyntaxKind.IdentifierToken);

        if (currentToken.kind == SyntaxKind.EqualsToken) {
            var equals = EatToken();
            var initializer = ParseExpression();
            var semicolon = Match(SyntaxKind.SemicolonToken);

            if (inStruct)
                equals = AddDiagnostic(equals, Error.CannotInitializeInStructs());

            return SyntaxFactory.VariableDeclarationStatement(type, identifier, equals, initializer, semicolon);
        } else {
            var semicolon = Match(SyntaxKind.SemicolonToken);

            return SyntaxFactory.VariableDeclarationStatement(type, identifier, semicolon);
        }
    }

    private StatementSyntax ParseWhileStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.WhileStatement(keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private StatementSyntax ParseForStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var initializer = ParseStatement();
        var condition = ParseNonAssignmentExpression();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        ExpressionSyntax step;
        if (currentToken.kind == SyntaxKind.CloseParenToken)
            step = SyntaxFactory.Empty();
        else
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.ForStatement(
            keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body
        );
    }

    private StatementSyntax ParseIfStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var then = ParseStatement();

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        var nestedIf = false;
        var inner = then;
        var offset = 0;

        while (inner.kind == SyntaxKind.IfStatement) {
            nestedIf = true;
            var innerIf = (IfStatementSyntax)inner;
            offset += innerIf.GetSlotOffset(4);

            if (innerIf.elseClause != null && innerIf.then.kind != SyntaxKind.BlockStatement) {
                var elseOffset = offset + innerIf.then.fullWidth + innerIf.elseClause.GetLeadingTriviaWidth();

                then = AddDiagnostic(
                    then,
                    Error.AmbiguousElse(),
                    elseOffset,
                    innerIf.elseClause.keyword.width
                );
            }

            if (innerIf.then.kind == SyntaxKind.IfStatement)
                inner = innerIf.then;
            else
                break;
        }

        var elseClause = ParseElseClause();

        if (elseClause != null && then.kind != SyntaxKind.BlockStatement && nestedIf) {
            elseClause = AddDiagnostic(
                elseClause,
                Error.AmbiguousElse(),
                elseClause.keyword.GetLeadingTriviaWidth(),
                elseClause.keyword.width
            );
        }

        return SyntaxFactory.IfStatement(keyword, openParenthesis, condition, closeParenthesis, then, elseClause);
    }

    private ElseClauseSyntax ParseElseClause() {
        if (currentToken.kind != SyntaxKind.ElseKeyword)
            return null;

        var keyword = Match(SyntaxKind.ElseKeyword);
        var statement = ParseStatement();

        return SyntaxFactory.ElseClause(keyword, statement);
    }

    private StatementSyntax ParseExpressionStatement() {
        var diagnosticCount = currentToken.GetDiagnostics().Length;
        var expression = ParseExpression(allowEmpty: true);
        var nextDiagnosticCount = currentToken.GetDiagnostics().Length;
        var semicolon = Match(SyntaxKind.SemicolonToken);

        if (nextDiagnosticCount > diagnosticCount && semicolon.containsDiagnostics) {
            var diagnostics = semicolon.GetDiagnostics();
            semicolon = semicolon.WithDiagnosticsGreen(diagnostics.SkipLast(1).ToArray());
        }

        return SyntaxFactory.ExpressionStatement(expression, semicolon);
    }

    private StatementSyntax ParseBlockStatement() {
        var statements = SyntaxListBuilder<StatementSyntax>.Create();
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var startToken = currentToken;

        while (currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.BlockStatement(openBrace, statements.ToList(), closeBrace);
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
                left = SyntaxFactory.AssignmentExpression(left, operatorToken, right);
                break;
            default:
                break;
        }

        return left;
    }

    private ExpressionSyntax ParseNonAssignmentExpression() {
        var saved = _context;
        _context |= ParserContext.InExpression;

        if (currentToken.kind == SyntaxKind.SemicolonToken)
            return ParseEmptyExpression();

        _expectParenthesis = true;
        var value = ParseOperatorExpression();
        _expectParenthesis = false;
        _context = saved;
        return value;
    }

    private ExpressionSyntax ParseExpression(bool allowEmpty = false) {
        var saved = _context;
        _context |= ParserContext.InExpression;

        if (currentToken.kind == SyntaxKind.SemicolonToken) {
            if (!allowEmpty)
                AddDiagnosticToNextToken(Error.ExpectedToken(SyntaxKind.IdentifierNameExpression));

            return ParseEmptyExpression();
        }

        var expression = ParseAssignmentExpression();
        _context = saved;
        return expression;
    }

    private ExpressionSyntax ParseEmptyExpression() {
        return SyntaxFactory.Empty();
    }

    private ExpressionSyntax ParseOperatorExpression(int parentPrecedence = 0) {
        ExpressionSyntax left;
        var unaryPrecedence = currentToken.kind.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence && !IsTerminator()) {
            var op = EatToken();

            if (op.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression();
                left = SyntaxFactory.PrefixExpression(op, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = SyntaxFactory.UnaryExpression(op, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            var precedence = currentToken.kind.GetBinaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence || IsTerminator())
                break;

            var op = EatToken();
            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.BinaryExpression(left, op, right);
        }

        while (true) {
            var precedence = currentToken.kind.GetTernaryPrecedence();

            if (precedence == 0 || precedence < parentPrecedence || IsTerminator())
                break;

            var leftOp = EatToken();
            var center = ParseOperatorExpression(precedence);
            var rightOp = Match(leftOp.kind.GetTernaryOperatorPair());
            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.TernaryExpression(left, leftOp, center, rightOp, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal() {
        if (PeekIsType(0, out _, out _, out var isTemplate) && isTemplate &&
            (_context & ParserContext.InTemplateArgumentList) != 0) {
            return ParseTypeExpression();
        }

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
            case SyntaxKind.NewKeyword:
                return ParseObjectCreationExpression();
            case SyntaxKind.ThisKeyword:
                return ParseThisExpression();
            case SyntaxKind.IdentifierToken:
            default:
                return ParseNameExpression();
        }
    }

    private ExpressionSyntax ParsePrimaryExpression(int parentPrecedence = 0, ExpressionSyntax left = null) {
        ExpressionSyntax ParseCorrectPrimaryOperator(ExpressionSyntax operand) {
            return currentToken.kind switch {
                SyntaxKind.OpenParenToken => ParseCallExpression(operand),
                SyntaxKind.OpenBracketToken or SyntaxKind.QuestionOpenBracketToken => ParseIndexExpression(operand),
                SyntaxKind.PeriodToken or SyntaxKind.QuestionPeriodToken => ParseMemberAccessExpression(operand),
                SyntaxKind.MinusMinusToken or SyntaxKind.PlusPlusToken or SyntaxKind.ExclamationToken => ParsePostfixExpression(operand),
                _ => operand,
            };
        }

        left ??= ParsePrimaryExpressionInternal();

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

        return SyntaxFactory.CastExpression(openParenthesis, type, closeParenthesis, expression);
    }

    private ExpressionSyntax ParseReferenceExpression() {
        var keyword = Match(SyntaxKind.RefKeyword);
        var identifier = Match(SyntaxKind.IdentifierToken);

        return SyntaxFactory.ReferenceExpression(keyword, identifier);
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax operand) {
        var op = EatToken();
        return SyntaxFactory.PostfixExpression(operand, op);
    }

    private ExpressionSyntax ParseInitializerListExpression() {
        var left = Match(SyntaxKind.OpenBraceToken);
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextItem = true;

        while (parseNextItem && currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            if (currentToken.kind is not SyntaxKind.CommaToken and not SyntaxKind.CloseBraceToken) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            } else {
                var empty = SyntaxFactory.Empty();
                nodesAndSeparators.Add(empty);
            }

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToList());
        var right = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.InitializerListExpression(left, separatedSyntaxList, right);
    }

    private ExpressionSyntax ParseParenthesizedExpression() {
        var left = Match(SyntaxKind.OpenParenToken);
        _bracketStack.Push(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();
        _bracketStack.Pop();
        var right = Match(SyntaxKind.CloseParenToken);


        return SyntaxFactory.ParenthesisExpression(left, expression, right);
    }

    private ExpressionSyntax ParseTypeOfExpression() {
        var keyword = Match(SyntaxKind.TypeOfKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.TypeOfExpression(keyword, openParenthesis, type, closeParenthesis);
    }

    private ExpressionSyntax ParseObjectCreationExpression() {
        var keyword = Match(SyntaxKind.NewKeyword);
        var type = ParseType(false);
        var argumentList = ParseArgumentList();

        return SyntaxFactory.ObjectCreationExpression(keyword, type, argumentList);
    }

    private ExpressionSyntax ParseThisExpression() {
        var keyword = Match(SyntaxKind.ThisKeyword);
        return SyntaxFactory.ThisExpression(keyword);
    }

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax operand) {
        var op = EatToken();
        var member = Match(SyntaxKind.IdentifierToken);

        return SyntaxFactory.MemberAccessExpression(operand, op, member);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax operand) {
        var openBracket = EatToken();
        _bracketStack.Push(SyntaxKind.CloseBracketToken);
        var index = ParseExpression();
        _bracketStack.Pop();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.IndexExpression(operand, openBracket, index, closeBracket);
    }

    private ExpressionSyntax ParseNameExpression() {
        if (currentToken.kind != SyntaxKind.IdentifierToken) {
            _currentToken = AddDiagnostic(currentToken, Error.ExpectedToken("expression"));
            return SyntaxFactory.IdentifierNameExpression(SyntaxFactory.Missing(SyntaxKind.IdentifierToken));
        }

        var identifier = EatToken();
        var kind = ScanTemplateArgumentList();

        // ! Temporary, template name expressions are not enabled until needed (when template methods get implemented)
        // Just remove `|| true` when you want to enable template name expressions
        if (kind == ScanTemplateArgumentListKind.NotTemplateArgumentList || true)
            return SyntaxFactory.IdentifierNameExpression(identifier);

        var templateArgumentList = ParseTemplateArgumentList();
        return SyntaxFactory.TemplateNameExpression(identifier, templateArgumentList);
    }

    private ScanTemplateArgumentListKind ScanTemplateArgumentList() {
        if (currentToken.kind != SyntaxKind.LessThanToken)
            return ScanTemplateArgumentListKind.NotTemplateArgumentList;

        if ((_context & ParserContext.InExpression) == 0)
            return ScanTemplateArgumentListKind.DefiniteTemplateArgumentList;

        var lookahead = 1;

        while (Peek(lookahead).kind is not SyntaxKind.GreaterThanToken and not SyntaxKind.EndOfFileToken)
            lookahead++;

        return Peek(lookahead + 1).kind switch {
            SyntaxKind.OpenParenToken or SyntaxKind.EndOfFileToken => ScanTemplateArgumentListKind.PossibleTemplateArgumentList,
            _ => ScanTemplateArgumentListKind.NotTemplateArgumentList,
        };
    }

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax operand) {
        if (operand is not MemberAccessExpressionSyntax and not NameExpressionSyntax)
            operand = AddDiagnostic(operand, Error.ExpectedMethodName());

        var argumentList = ParseArgumentList();
        return SyntaxFactory.CallExpression(operand, argumentList);
    }

    private TemplateArgumentListSyntax ParseTemplateArgumentList() {
        var openAngleBracket = Match(SyntaxKind.LessThanToken);

        _bracketStack.Push(SyntaxKind.GreaterThanToken);
        var savedTerminatorState = _terminatorState;
        var savedContext = _context;
        _terminatorState |= TerminatorState.IsEndOfTemplateArgumentList;
        _context |= ParserContext.InTemplateArgumentList;
        var arguments = ParseArguments();
        _terminatorState = savedTerminatorState;
        _context = savedContext;
        _bracketStack.Pop();

        var closeAngleBracket = Match(SyntaxKind.GreaterThanToken);

        return SyntaxFactory.TemplateArgumentList(openAngleBracket, arguments, closeAngleBracket);
    }

    private ArgumentListSyntax ParseArgumentList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var arguments = ParseArguments();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ArgumentList(openParenthesis, arguments, closeParenthesis);
    }

    private SeparatedSyntaxList<ArgumentSyntax> ParseArguments() {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextArgument = true;

        if (currentToken.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && currentToken.kind != SyntaxKind.EndOfFileToken) {
                if (currentToken.kind is not SyntaxKind.CommaToken and not SyntaxKind.CloseParenToken) {
                    var argument = ParseArgument();
                    nodesAndSeparators.Add(argument);
                } else {
                    var empty = SyntaxFactory.Argument(
                        null,
                        null,
                        SyntaxFactory.Empty()
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

        return new SeparatedSyntaxList<ArgumentSyntax>(nodesAndSeparators.ToList());
    }

    private ArgumentSyntax ParseArgument() {
        SyntaxToken name = null;
        SyntaxToken colon = null;

        if (currentToken.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.ColonToken) {
            name = EatToken();
            colon = Match(SyntaxKind.ColonToken);
        }

        ExpressionSyntax expression;
        if (currentToken.kind is SyntaxKind.CommaToken or SyntaxKind.CloseParenToken)
            expression = SyntaxFactory.Empty();
        else
            expression = ParseNonAssignmentExpression();

        return SyntaxFactory.Argument(name, colon, expression);
    }

    private SyntaxList<AttributeSyntax> ParseAttributes() {
        var attributes = SyntaxListBuilder<AttributeSyntax>.Create();

        while (currentToken.kind == SyntaxKind.OpenBracketToken)
            attributes.Add(ParseAttribute());

        return attributes.ToList();
    }

    private AttributeSyntax ParseAttribute() {
        var openBracket = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.Attribute(openBracket, identifier, closeBracket);
    }

    private TypeExpressionSyntax ParseTypeExpression() {
        var type = ParseType(expectName: false, declarationOnly: true);
        return SyntaxFactory.TypeExpression(type);
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
                refKeyword = AddDiagnostic(refKeyword, Error.CannotUseRef());
        }

        if (currentToken.kind == SyntaxKind.ConstKeyword) {
            constKeyword = EatToken();

            if (declarationOnly) {
                constKeyword = AddDiagnostic(constKeyword, Error.CannotUseConst());
            } else if (!allowImplicit &&
                    (Peek(1).kind is not SyntaxKind.IdentifierToken and not SyntaxKind.OpenBracketToken)) {
                constKeyword = AddDiagnostic(constKeyword, Error.CannotUseImplicit());
            }
        }

        if (currentToken.kind == SyntaxKind.VarKeyword) {
            varKeyword = EatToken();

            if (!allowImplicit)
                varKeyword = AddDiagnostic(varKeyword, Error.CannotUseImplicit());
        }

        var hasTypeName = varKeyword is null &&
            (!allowImplicit ||
                constKeyword is null || Peek(1).kind is SyntaxKind.IdentifierToken or SyntaxKind.OpenBracketToken
            )
        ;

        if (hasTypeName)
            typeName = Match(SyntaxKind.IdentifierToken);

        TemplateArgumentListSyntax templateArgumentList = null;

        if (currentToken.kind == SyntaxKind.LessThanToken)
            templateArgumentList = ParseTemplateArgumentList();

        var rankSpecifiers = SyntaxListBuilder<ArrayRankSpecifierSyntax>.Create();

        while (currentToken.kind == SyntaxKind.OpenBracketToken) {
            var arrayRankSpecifier = ParseArrayRankSpecifierSyntax();
            rankSpecifiers.Add(arrayRankSpecifier);
        }

        return SyntaxFactory.Type(
            attributes,
            constRefKeyword,
            refKeyword,
            constKeyword,
            varKeyword,
            typeName,
            templateArgumentList,
            rankSpecifiers.ToList()
        );
    }

    private ExpressionSyntax ParseNullLiteral() {
        var token = Match(SyntaxKind.NullKeyword);
        return SyntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseNumericLiteral() {
        var token = Match(SyntaxKind.NumericLiteralToken);
        return SyntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseBooleanLiteral() {
        var isTrue = currentToken.kind == SyntaxKind.TrueKeyword;
        var keyword = isTrue ? Match(SyntaxKind.TrueKeyword) : Match(SyntaxKind.FalseKeyword);
        return SyntaxFactory.Literal(keyword, isTrue);
    }

    private ExpressionSyntax ParseStringLiteral() {
        var stringToken = Match(SyntaxKind.StringLiteralToken);
        return SyntaxFactory.Literal(stringToken);
    }

    private ArrayRankSpecifierSyntax ParseArrayRankSpecifierSyntax() {
        var openBracket = Match(SyntaxKind.OpenBracketToken);
        var closeBracket = Match(SyntaxKind.CloseBracketToken);
        return SyntaxFactory.ArrayRankSpecifier(openBracket, closeBracket);
    }
}
