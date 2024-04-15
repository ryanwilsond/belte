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

    internal SyntaxToken currentToken {
        get {
            if (_currentToken is null)
                _currentToken = FetchCurrentToken();

            return _currentToken;
        }
    }

    // Used for reusing nodes from the old tree. Just validate and call EatNode to reuse an old node.
    internal SyntaxNode currentNode {
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

    private ResetPoint GetResetPoint() {
        return new ResetPoint(_tokenOffset, _prevTokenTrailingTrivia, _terminatorState, _context, _bracketStack);
    }

    private void Reset(ResetPoint resetPoint) {
        _terminatorState = resetPoint.terminatorState;
        _tokenOffset = resetPoint.position;
        _prevTokenTrailingTrivia = resetPoint.prevTokenTrailingTrivia;
        _currentToken = null;
        _currentNode = null;
        _context = resetPoint.context;
        _bracketStack = resetPoint.bracketStack;

        if (_blendedTokens != null) {
            for (var i = _tokenOffset; i < _tokenCount; i++) {
                if (_blendedTokens[i].token == null) {
                    _tokenCount = i;

                    if (_tokenCount == _tokenOffset)
                        FetchCurrentToken();

                    break;
                }
            }
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

    private bool PeekIsLocalDeclaration() {
        var offset = 0;

        while (Peek(offset).kind == SyntaxKind.OpenBracketToken) {
            offset++;

            while (Peek(offset).kind is SyntaxKind.IdentifierToken or SyntaxKind.CommaToken)
                offset++;

            if (Peek(offset).kind == SyntaxKind.CloseBracketToken)
                offset++;
        }

        var hasConstKeyword = false;

        while (Peek(offset).kind is SyntaxKind.ConstexprKeyword or SyntaxKind.ConstKeyword) {
            offset++;
            hasConstKeyword = true;
        }

        return PeekIsType(offset, out _, out var hasName, out _) && (hasName || hasConstKeyword);
    }

    private bool PeekIsType(int offset, out int finalOffset, out bool hasName, out bool isTemplate) {
        finalOffset = offset;
        hasName = false;
        isTemplate = false;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken and not SyntaxKind.RefKeyword)
            return false;

        while (Peek(finalOffset).kind is SyntaxKind.ConstKeyword or SyntaxKind.RefKeyword)
            finalOffset++;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken &&
            Peek(finalOffset - 1).kind != SyntaxKind.ConstKeyword) {
            return false;
        }

        if (Peek(finalOffset).kind is SyntaxKind.IdentifierToken)
            finalOffset++;

        while (Peek(finalOffset).kind == SyntaxKind.LessThanToken) {
            isTemplate = true;
            finalOffset++;

            while (Peek(finalOffset).kind is not SyntaxKind.GreaterThanToken and not SyntaxKind.EndOfFileToken)
                finalOffset++;

            finalOffset++;
        }

        var hasBrackets = false;
        var bracketsBeenClosed = true;

        while (Peek(finalOffset).kind is SyntaxKind.OpenBracketToken or SyntaxKind.CloseBracketToken) {
            hasBrackets = true;

            if (Peek(finalOffset).kind is SyntaxKind.OpenBracketToken)
                bracketsBeenClosed = false;
            if (Peek(finalOffset).kind is SyntaxKind.CloseBracketToken)
                bracketsBeenClosed = true;

            finalOffset++;
        }

        if (Peek(finalOffset).kind is SyntaxKind.ExclamationToken)
            finalOffset++;

        if (Peek(finalOffset).kind is SyntaxKind.IdentifierToken && bracketsBeenClosed)
            hasName = true;

        if (!hasBrackets &&
            Peek(finalOffset).kind != SyntaxKind.IdentifierToken &&
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

    private SyntaxList<MemberDeclarationSyntax> ParseMembers(bool isGlobal = false) {
        var members = SyntaxListBuilder<MemberDeclarationSyntax>.Create();

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

    private bool TryParseMember(bool allowGlobalStatements, out MemberDeclarationSyntax member) {
        if (currentToken.kind == SyntaxKind.BadToken) {
            member = null;
            return false;
        }

        member = ParseMember(allowGlobalStatements);
        return true;
    }

    private MemberDeclarationSyntax ParseMember(bool allowGlobalStatements = false) {
        var attributeLists = ParseAttributeLists();
        var modifiers = ParseModifiers();

        if ((_context & ParserContext.InClassDefinition) != 0 &&
            PeekIsFunctionOrMethodDeclaration(checkForType: false)) {
            return ParseConstructorDeclaration(attributeLists, modifiers);
        }

        if (PeekIsFunctionOrMethodDeclaration(couldBeInStatement: allowGlobalStatements))
            return ParseMethodDeclaration(attributeLists, modifiers);

        switch (currentToken.kind) {
            case SyntaxKind.StructKeyword:
                return ParseStructDeclaration(attributeLists, modifiers);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(attributeLists, modifiers);
            default:
                if (allowGlobalStatements) {
                    if (attributeLists.Any()) {
                        var builder = new SyntaxListBuilder<AttributeListSyntax>(attributeLists.Count);

                        for (var i = 0; i < attributeLists.Count; i++) {
                            if (i == 0)
                                builder.Add(AddDiagnostic(attributeLists[i], Error.InvalidAttributes()));
                            else
                                builder.Add(attributeLists[i]);
                        }

                        attributeLists = builder.ToList();
                    }

                    if (modifiers.Any()) {
                        var builder = new SyntaxListBuilder<SyntaxToken>(modifiers.Count);

                        foreach (var modifier in modifiers) {
                            if (modifier.kind == SyntaxKind.ConstKeyword) {
                                builder.Add(modifier);
                                continue;
                            }

                            builder.Add(
                                AddDiagnostic(modifier, Error.InvalidModifier(SyntaxFacts.GetText(modifier.kind)))
                            );
                        }

                        modifiers = builder.ToList();
                    }

                    return ParseGlobalStatement(attributeLists, modifiers);
                } else {
                    return ParseFieldDeclaration(attributeLists, modifiers);
                }
        }
    }

    private MemberDeclarationSyntax ParseStructDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
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
            attributeLists,
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace
        );
    }

    private MemberDeclarationSyntax ParseClassDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
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
            attributeLists,
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace
        );
    }

    private ConstructorDeclarationSyntax ParseConstructorDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.ConstructorDeclaration(attributeLists, modifiers, identifier, parameterList, body);
    }

    private MemberDeclarationSyntax ParseMethodDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.MethodDeclaration(attributeLists, modifiers, type, identifier, parameterList, body);
    }

    private StatementSyntax ParseLocalFunctionDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        attributeLists ??= ParseAttributeLists();
        modifiers ??= ParseModifiers();
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var parameters = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.LocalFunctionStatement(
            attributeLists,
            modifiers,
            type,
            identifier,
            parameters,
            body
        );
    }

    private DeclarationModifiers GetModifier(SyntaxToken token) {
        return token.kind switch {
            SyntaxKind.StaticKeyword => DeclarationModifiers.Static,
            SyntaxKind.ConstKeyword => DeclarationModifiers.Const,
            SyntaxKind.ConstexprKeyword => DeclarationModifiers.Constexpr,
            _ => DeclarationModifiers.None,
        };
    }

    private SyntaxList<SyntaxToken> ParseModifiers() {
        var modifiers = SyntaxListBuilder<SyntaxToken>.Create();

        while (true) {
            var modifier = GetModifier(currentToken);

            if (modifier == DeclarationModifiers.None)
                break;

            modifiers.Add(EatToken());
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

    private SyntaxList<MemberDeclarationSyntax> ParseFieldList() {
        var fieldDeclarations = SyntaxListBuilder<MemberDeclarationSyntax>.Create();

        while (currentToken.kind is not SyntaxKind.CloseBraceToken and not SyntaxKind.EndOfFileToken) {
            var attributeLists = ParseAttributeLists();
            var modifiers = ParseModifiers();
            var field = ParseFieldDeclaration(attributeLists, modifiers);
            fieldDeclarations.Add(field);
        }

        return fieldDeclarations.ToList();
    }

    private FieldDeclarationSyntax ParseFieldDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var declaration = ParseVariableDeclaration(false);
        var semicolon = Match(SyntaxKind.SemicolonToken);
        return SyntaxFactory.FieldDeclaration(attributeLists, modifiers, declaration, semicolon);
    }

    private MemberDeclarationSyntax ParseGlobalStatement(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var statement = ParseStatementCore(
            attributeLists,
            modifiers,
            out var consumedAttributeLists,
            out var consumedModifiers
        );

        if (consumedAttributeLists)
            attributeLists = null;
        if (consumedModifiers)
            modifiers = null;

        return SyntaxFactory.GlobalStatement(attributeLists, modifiers, statement);
    }

    private VariableDeclarationSyntax ParseVariableDeclaration(
        bool allowImplicit = true,
        bool hasConstKeyword = false) {
        var inStruct = (_context & ParserContext.InStructDefinition) != 0;
        var type = ParseType(allowImplicit: allowImplicit, allowRef: !inStruct, hasConstKeyword: hasConstKeyword);
        var identifier = Match(SyntaxKind.IdentifierToken);
        EqualsValueClauseSyntax initializer = null;

        if (currentToken.kind == SyntaxKind.EqualsToken)
            initializer = ParseEqualsValueClause(inStruct);

        return SyntaxFactory.VariableDeclaration(type, identifier, initializer);
    }

    private EqualsValueClauseSyntax ParseEqualsValueClause(bool inStruct) {
        var equals = EatToken();
        var value = ParseExpression();

        if (inStruct)
            equals = AddDiagnostic(equals, Error.CannotInitializeInStructs());

        return SyntaxFactory.EqualsValueClause(equals, value);
    }

    private StatementSyntax ParseStatement() {
        return ParseStatementCore(null, null, out _, out _);
    }

    private StatementSyntax ParseStatementCore(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        out bool consumedAttributeLists,
        out bool consumedModifiers) {
        var saved = _context;
        _context |= ParserContext.InStatement;

        var statement = ParseStatementInternal(
            attributeLists,
            modifiers,
            out consumedAttributeLists,
            out consumedModifiers
        );

        _context = saved;

        return statement;
    }

    private StatementSyntax ParseStatementInternal(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        out bool consumedAttributeLists,
        out bool consumedModifiers) {
        consumedAttributeLists = false;
        consumedModifiers = false;

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
        }

        if (PeekIsFunctionOrMethodDeclaration()) {
            consumedAttributeLists = true;
            consumedModifiers = true;
            return ParseLocalFunctionDeclaration(attributeLists, modifiers);
        }

        if (PeekIsLocalDeclaration()) {
            consumedAttributeLists = true;
            consumedModifiers = true;
            return ParseLocalDeclarationStatement(attributeLists, modifiers);
        }

        return ParseExpressionStatement();
    }

    private StatementSyntax ParseLocalDeclarationStatement(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        attributeLists ??= ParseAttributeLists();
        modifiers ??= ParseModifiers();
        var hasConstKeyword = false;

        foreach (var modifier in modifiers) {
            if (modifier.kind == SyntaxKind.ConstKeyword) {
                hasConstKeyword = true;
                break;
            }
        }

        var declaration = ParseVariableDeclaration(hasConstKeyword: hasConstKeyword);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.LocalDeclarationStatement(attributeLists, modifiers, declaration, semicolon);
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
                AddDiagnosticToNextToken(Error.ExpectedToken(SyntaxKind.IdentifierName));

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
            var operatorToken = EatToken();

            if (operatorToken.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression();
                left = SyntaxFactory.PrefixExpression(operatorToken, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = SyntaxFactory.UnaryExpression(operatorToken, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            var precedence = currentToken.kind.GetBinaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence || IsTerminator())
                break;

            var operatorToken = EatToken();
            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.BinaryExpression(left, operatorToken, right);
        }

        while (true) {
            var precedence = currentToken.kind.GetTernaryPrecedence();

            if (precedence == 0 || precedence < parentPrecedence || IsTerminator())
                break;

            var leftOperatorToken = EatToken();
            var center = ParseOperatorExpression(precedence);
            var rightOperatorToken = Match(leftOperatorToken.kind.GetTernaryOperatorPair());
            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.TernaryExpression(left, leftOperatorToken, center, rightOperatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal() {
        if (PeekIsType(0, out _, out _, out var isTemplate) && isTemplate &&
            (_context & ParserContext.InTemplateArgumentList) != 0) {
            return ParseQualifiedName();
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
                return ParseLastCaseName();
        }
    }

    private ExpressionSyntax ParsePrimaryExpression(int parentPrecedence = 0, ExpressionSyntax left = null) {
        ExpressionSyntax ParseCorrectPrimaryOperator(ExpressionSyntax expression) {
            return currentToken.kind switch {
                SyntaxKind.OpenParenToken => ParseCallExpression(expression),
                SyntaxKind.OpenBracketToken or SyntaxKind.QuestionOpenBracketToken => ParseIndexExpression(expression),
                SyntaxKind.PeriodToken or SyntaxKind.QuestionPeriodToken => ParseMemberAccessExpression(expression),
                SyntaxKind.MinusMinusToken or SyntaxKind.PlusPlusToken or SyntaxKind.ExclamationToken => ParsePostfixExpression(expression),
                _ => expression,
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
        var type = ParseType(false, false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();

        return SyntaxFactory.CastExpression(openParenthesis, type, closeParenthesis, expression);
    }

    private ExpressionSyntax ParseReferenceExpression() {
        var keyword = Match(SyntaxKind.RefKeyword);
        var expression = ParseExpression();

        return SyntaxFactory.ReferenceExpression(keyword, expression);
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax operand) {
        var operatorToken = EatToken();
        return SyntaxFactory.PostfixExpression(operand, operatorToken);
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

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax expression) {
        var operatorToken = EatToken();
        var name = ParseSimpleName();

        return SyntaxFactory.MemberAccessExpression(expression, operatorToken, name);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax expression) {
        var openBracket = EatToken();
        _bracketStack.Push(SyntaxKind.CloseBracketToken);
        var index = ParseExpression();
        _bracketStack.Pop();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.IndexExpression(expression, openBracket, index, closeBracket);
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

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax expression) {
        var argumentList = ParseArgumentList();
        return SyntaxFactory.CallExpression(expression, argumentList);
    }

    private TemplateArgumentListSyntax ParseTemplateArgumentList() {
        var openAngleBracket = Match(SyntaxKind.LessThanToken);

        _bracketStack.Push(SyntaxKind.GreaterThanToken);
        var savedTerminatorState = _terminatorState;
        var savedContext = _context;
        _terminatorState |= TerminatorState.IsEndOfTemplateArgumentList;
        _context |= ParserContext.InTemplateArgumentList;
        var arguments = ParseArguments(SyntaxKind.GreaterThanToken);
        _terminatorState = savedTerminatorState;
        _context = savedContext;
        _bracketStack.Pop();

        var closeAngleBracket = Match(SyntaxKind.GreaterThanToken);

        return SyntaxFactory.TemplateArgumentList(openAngleBracket, arguments, closeAngleBracket);
    }

    private ArgumentListSyntax ParseArgumentList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var arguments = ParseArguments(SyntaxKind.CloseParenToken);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ArgumentList(openParenthesis, arguments, closeParenthesis);
    }

    private SeparatedSyntaxList<ArgumentSyntax> ParseArguments(SyntaxKind closeBracket) {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextArgument = true;

        if (currentToken.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && currentToken.kind != SyntaxKind.EndOfFileToken) {
                if (currentToken.kind != SyntaxKind.CommaToken && currentToken.kind != closeBracket) {
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

    private SyntaxList<AttributeListSyntax> ParseAttributeLists() {
        var attributeLists = SyntaxListBuilder<AttributeListSyntax>.Create();

        while (currentToken.kind == SyntaxKind.OpenBracketToken)
            attributeLists.Add(ParseAttributeList());

        return attributeLists.ToList();
    }

    private AttributeListSyntax ParseAttributeList() {
        var openBracket = EatToken();

        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextAttribute = true;

        while (parseNextAttribute &&
            currentToken.kind != SyntaxKind.CloseBracketToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var attribute = ParseAttribute();
            nodesAndSeparators.Add(attribute);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextAttribute = false;
            }
        }

        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.AttributeList(
            openBracket,
            new SeparatedSyntaxList<AttributeSyntax>(nodesAndSeparators.ToList()),
            closeBracket
        );
    }

    private AttributeSyntax ParseAttribute() {
        var identifier = Match(SyntaxKind.IdentifierToken);
        return SyntaxFactory.Attribute(identifier);
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

    private ArrayRankSpecifierSyntax ParseArrayRankSpecifier() {
        var openBracket = Match(SyntaxKind.OpenBracketToken);
        var closeBracket = Match(SyntaxKind.CloseBracketToken);
        return SyntaxFactory.ArrayRankSpecifier(openBracket, closeBracket);
    }

    private SimpleNameSyntax ParseLastCaseName() {
        if (currentToken.kind != SyntaxKind.IdentifierToken) {
            _currentToken = AddDiagnostic(currentToken, Error.ExpectedToken("expression"));
            return SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken));
        }

        return ParseSimpleName();
    }

    private SimpleNameSyntax ParseSimpleName() {
        var identifierName = ParseIdentifierName();

        if (identifierName.identifier.isFabricated)
            return identifierName;

        SimpleNameSyntax name = identifierName;

        if (currentToken.kind == SyntaxKind.LessThanToken) {
            if ((_context & ParserContext.InExpression) != 0) {
                // If we are in an expression, check if we are truly a template name.
                // If any issues while parsing the template name, abort and treat the '<' as a binary operator.
                var point = GetResetPoint();
                var templateArgumentList = ParseTemplateArgumentList();

                if (templateArgumentList.containsDiagnostics)
                    Reset(point);
                else
                    name = SyntaxFactory.TemplateName(identifierName.identifier, templateArgumentList);
            }
        }

        return name;
    }

    private IdentifierNameSyntax ParseIdentifierName() {
        var identifier = Match(SyntaxKind.IdentifierToken);
        return SyntaxFactory.IdentifierName(identifier);
    }

    private TypeSyntax ParseType(bool allowImplicit = true, bool allowRef = true, bool hasConstKeyword = false) {
        if (currentToken.kind == SyntaxKind.RefKeyword) {
            var refKeyword = EatToken();

            if (!allowRef)
                refKeyword = AddDiagnostic(refKeyword, Error.CannotUseRef());

            return SyntaxFactory.ReferenceType(
                refKeyword,
                currentToken.kind == SyntaxKind.ConstKeyword ? EatToken() : null,
                ParseTypeCore(allowImplicit && hasConstKeyword)
            );
        }

        return ParseTypeCore(allowImplicit && hasConstKeyword);
    }

    private TypeSyntax ParseTypeCore(bool constAsType) {
        TypeSyntax type;

        if (currentToken.kind is SyntaxKind.ExclamationToken or SyntaxKind.OpenBracketToken ||
            (currentToken.kind == SyntaxKind.IdentifierToken &&
             Peek(1).kind is SyntaxKind.EqualsToken or SyntaxKind.SemicolonToken)) {
            type = SyntaxFactory.EmptyName();
        } else {
            type = ParseUnderlyingType();
        }

        var lastTokenPosition = -1;

        while (_tokenOffset > lastTokenPosition) {
            lastTokenPosition = _tokenOffset;

            switch (currentToken.kind) {
                case SyntaxKind.ExclamationToken:
                    var exclamationToken = EatToken();
                    type = SyntaxFactory.NonNullableType(type, exclamationToken);
                    goto done;
                case SyntaxKind.OpenBracketToken:
                    var rankSpecifiers = SyntaxListBuilder<ArrayRankSpecifierSyntax>.Create();

                    do {
                        rankSpecifiers.Add(ParseArrayRankSpecifier());
                    } while (currentToken.kind == SyntaxKind.OpenBracketToken);

                    type = SyntaxFactory.ArrayType(type, rankSpecifiers.ToList());
                    continue;
            }
        }

done:
        return type;
    }

    private TypeSyntax ParseUnderlyingType() {
        if (currentToken.kind == SyntaxKind.IdentifierToken)
            return ParseQualifiedName();

        return AddDiagnostic(
            WithFutureDiagnostics(SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken))),
            Error.ExpectedToken(SyntaxKind.IdentifierName),
            currentToken.GetLeadingTriviaWidth(),
            currentToken.width
        );
    }

    private NameSyntax ParseQualifiedName() {
        NameSyntax name = ParseSimpleName();

        while (currentToken.kind == SyntaxKind.PeriodToken) {
            var separator = EatToken();
            name = ParseQualifiedNameRight(name, separator);
        }

        return name;
    }

    private NameSyntax ParseQualifiedNameRight(NameSyntax left, SyntaxToken separator) {
        var right = ParseSimpleName();
        return SyntaxFactory.QualifiedName(left, separator, right);
    }
}
