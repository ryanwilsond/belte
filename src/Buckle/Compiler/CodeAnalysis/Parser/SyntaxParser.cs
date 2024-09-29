using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract partial class SyntaxParser {
    private static readonly ObjectPool<BlendedNode[]> BlendedNodesPool =
        new ObjectPool<BlendedNode[]>(() => new BlendedNode[32], 2);

    private protected readonly Lexer _lexer;
    private readonly LexerMode _mode;
    private readonly bool _isIncremental;
    private readonly SyntaxTree _syntaxTree;
    private readonly Blender _firstBlender;
    private readonly List<Diagnostic> _futureDiagnostics;

    private protected SyntaxToken _currentToken;
    private ArrayElement<SyntaxToken>[] _lexedTokens;
    private BlendedNode[] _blendedTokens;
    private BlendedNode _currentNode;
    private GreenNode _prevTokenTrailingTrivia;
    private int _tokenOffset;
    private int _tokenCount;

    private protected SyntaxParser(
        Lexer lexer,
        LexerMode mode,
        SyntaxNode oldTree,
        IEnumerable<TextChangeRange> changes,
        bool preLexIfNotIncremental) {
        _futureDiagnostics = new List<Diagnostic>();
        _syntaxTree = lexer.syntaxTree;
        _lexer = lexer;
        _mode = mode;
        _isIncremental = oldTree != null;

        if (_isIncremental) {
            _firstBlender = new Blender(_lexer, oldTree, changes);
            _blendedTokens = BlendedNodesPool.Allocate();
        } else {
            _firstBlender = null;
            _lexedTokens = new ArrayElement<SyntaxToken>[32];
        }

        if (preLexIfNotIncremental && !_isIncremental)
            PreLex();
    }

    internal SyntaxToken currentToken {
        get {
            _currentToken ??= FetchCurrentToken();
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

    internal DirectiveStack directives => _lexer.directives;

    private void PreLex() {
        var size = Math.Min(4096, Math.Max(32, _syntaxTree.text.length / 2));
        _lexedTokens = new ArrayElement<SyntaxToken>[size];

        for (var i = 0; i < size; i++) {
            var token = _lexer.LexNext(_mode);

            AddLexedToken(token);

            if (token.kind == SyntaxKind.EndOfFileToken)
                break;
        }
    }

    private protected static bool NoTriviaBetween(SyntaxToken token1, SyntaxToken token2) {
        return token1.GetTrailingTriviaWidth() == 0 && token2.GetLeadingTriviaWidth() == 0;
    }

    private protected ResetPoint GetResetPoint() {
        return new ResetPoint(_tokenOffset, _prevTokenTrailingTrivia);
    }

    private protected void Reset(ResetPoint resetPoint) {
        _tokenOffset = resetPoint.position;
        _prevTokenTrailingTrivia = resetPoint.prevTokenTrailingTrivia;
        _currentToken = null;
        _currentNode = null;

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

    private protected T AddDiagnostic<T>(T node, Diagnostic diagnostic) where T : BelteSyntaxNode {
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
                if (trivia.kind == SyntaxKind.SkippedTokensTrivia) {
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

    private protected T AddDiagnostic<T>(T node, Diagnostic diagnostic, int offset, int width) where T : BelteSyntaxNode {
        return WithAdditionalDiagnostics(node, new SyntaxDiagnostic(diagnostic, offset, width));
    }

    private protected T WithFutureDiagnostics<T>(T node) where T : BelteSyntaxNode {
        if (_futureDiagnostics.Count == 0)
            return node;

        var diagnostics = new SyntaxDiagnostic[_futureDiagnostics.Count];

        for (var i = 0; i < _futureDiagnostics.Count; i++)
            diagnostics[i] = new SyntaxDiagnostic(_futureDiagnostics[i], node.GetLeadingTriviaWidth(), node.width);

        _futureDiagnostics.Clear();
        return WithAdditionalDiagnostics(node, diagnostics);
    }

    private protected T WithAdditionalDiagnostics<T>(T node, params Diagnostic[] diagnostics) where T : BelteSyntaxNode {
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

    private protected void GetDiagnosticSpanForMissingToken(out int offset, out int width) {
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

    private protected T AddLeadingSkippedSyntax<T>(T node, GreenNode skippedSyntax) where T : BelteSyntaxNode {
        var oldToken = (node as SyntaxToken) ?? (SyntaxToken)node.GetFirstTerminal();
        var newToken = AddSkippedSyntax(oldToken, skippedSyntax, isTrailing: false);
        return SyntaxFirstTokenReplacer.Replace(node, newToken, skippedSyntax.fullWidth);
    }

    private protected SyntaxToken AddSkippedSyntax(SyntaxToken target, GreenNode skippedSyntax, bool isTrailing) {
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

                    builder.Add(SyntaxFactory.SkippedTokensTrivia(tk));
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

    private protected void AddDiagnosticToNextToken(Diagnostic diagnostic) {
        _futureDiagnostics.Add(diagnostic);
    }

    private protected SyntaxNode EatNode() {
        var saved = currentNode;

        if (_tokenOffset >= _blendedTokens.Length)
            AddTokenSlot();

        _blendedTokens[_tokenOffset++] = _currentNode;
        _tokenCount = _tokenOffset;

        _currentNode = null;
        _currentToken = null;

        return saved;
    }

    private protected SyntaxToken EatToken(bool stallDiagnostics = false) {
        var saved = currentToken;

        if (!stallDiagnostics)
            saved = WithFutureDiagnostics(saved);

        MoveToNextToken();
        return saved;
    }

    private protected void ReadCurrentNode() {
        if (_tokenOffset == 0)
            _currentNode = _firstBlender.ReadNode();
        else
            _currentNode = _blendedTokens[_tokenOffset - 1].blender.ReadNode();
    }

    private protected SyntaxToken FetchCurrentToken() {
        if (_tokenOffset >= _tokenCount)
            AddNewToken();

        if (_blendedTokens != null)
            return _blendedTokens[_tokenOffset].token;
        else
            return _lexedTokens[_tokenOffset];
    }

    private protected void AddLexedToken(SyntaxToken token) {
        if (_tokenCount >= _lexedTokens.Length) {
            var temp = new ArrayElement<SyntaxToken>[_lexedTokens.Length * 2];
            Array.Copy(_lexedTokens, temp, _lexedTokens.Length);
            _lexedTokens = temp;
        }

        _lexedTokens[_tokenCount].value = token;
        _tokenCount++;
    }

    private protected void AddNewToken() {
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
            AddLexedToken(_lexer.LexNext(_mode));
        }
    }

    private protected void AddToken(in BlendedNode token) {
        if (_tokenCount >= _blendedTokens.Length)
            AddTokenSlot();

        _blendedTokens[_tokenCount] = token;
        _tokenCount++;
    }

    private protected void AddTokenSlot() {
        var old = _blendedTokens;
        Array.Resize(ref _blendedTokens, _blendedTokens.Length * 2);
        BlendedNodesPool.ForgetTrackedObject(old, replacement: _blendedTokens);
    }

    private protected SyntaxToken Match(SyntaxKind kind, SyntaxKind? nextWanted = null, bool report = true) {
        if (currentToken.kind == kind)
            return EatToken();

        if (nextWanted != null && currentToken.kind == nextWanted) {
            if (report) {
                return AddDiagnostic(
                    WithFutureDiagnostics(SyntaxFactory.Missing(kind)),
                    Error.ExpectedToken(kind),
                    currentToken.GetLeadingTriviaWidth(),
                    currentToken.width
                );
            } else {
                return WithFutureDiagnostics(SyntaxFactory.Missing(kind));
            }
        }

        if (Peek(1).kind != kind) {
            var unexpectedToken = EatToken();

            if (report) {
                return AddDiagnostic(
                    AddLeadingSkippedSyntax(SyntaxFactory.Missing(kind), unexpectedToken),
                    Error.UnexpectedToken(unexpectedToken.kind, kind),
                    unexpectedToken.GetLeadingTriviaWidth(),
                    unexpectedToken.width
                );
            } else {
                return AddLeadingSkippedSyntax(SyntaxFactory.Missing(kind), unexpectedToken);
            }
        }

        var unexpected = EatToken(stallDiagnostics: true);

        if (report) {
            return AddDiagnostic(
                WithFutureDiagnostics(AddLeadingSkippedSyntax(EatToken(), unexpected)),
                Error.UnexpectedToken(unexpected.kind),
                unexpected.GetLeadingTriviaWidth(),
                unexpected.width
            );
        } else {
            return WithFutureDiagnostics(AddLeadingSkippedSyntax(EatToken(), unexpected));
        }
    }

    private protected SyntaxToken MatchTwo(SyntaxKind kind1, SyntaxKind kind2, bool report = true) {
        if (currentToken.kind == kind1)
            return Match(kind1, report: report);
        else if (currentToken.kind == kind2)
            return Match(kind2, report: report);

        var peek = Peek(1);

        if (peek.kind != kind1 && peek.kind != kind2) {
            var unexpectedToken = EatToken();

            if (report) {
                return AddDiagnostic(
                    AddLeadingSkippedSyntax(SyntaxFactory.Missing(kind1), unexpectedToken),
                    Error.UnexpectedToken(unexpectedToken.kind, kind1, kind2),
                    unexpectedToken.GetLeadingTriviaWidth(),
                    unexpectedToken.width
                );
            } else {
                return AddLeadingSkippedSyntax(SyntaxFactory.Missing(kind1), unexpectedToken);
            }
        }

        var unexpected = EatToken(stallDiagnostics: true);

        if (report) {
            return AddDiagnostic(
                WithFutureDiagnostics(AddLeadingSkippedSyntax(EatToken(), unexpected)),
                Error.UnexpectedToken(unexpected.kind),
                unexpected.GetLeadingTriviaWidth(),
                unexpected.width
            );
        } else {
            return WithFutureDiagnostics(AddLeadingSkippedSyntax(EatToken(), unexpected));
        }
    }

    private protected void MoveToNextToken() {
        _prevTokenTrailingTrivia = _currentToken.GetTrailingTrivia();
        _currentToken = null;

        if (_blendedTokens != null)
            _currentNode = null;

        _tokenOffset++;
    }

    private protected SyntaxToken Peek(int offset) {
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

    private protected bool IsMakingProgress(ref int lastTokenPosition) {
        if (_tokenOffset > lastTokenPosition) {
            lastTokenPosition = _tokenOffset;
            return true;
        }

        return false;
    }
}
