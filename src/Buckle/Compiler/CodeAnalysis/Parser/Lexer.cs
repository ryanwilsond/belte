using System;
using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Converts source text into parsable SyntaxTokens.<br/>
/// E.g.
/// <code>
/// int myInt;
/// --->
/// IdentifierToken IdentifierToken SemicolonToken
/// </code>
/// </summary>
internal sealed class Lexer {
    private readonly SourceText _text;
    private readonly List<SyntaxDiagnostic> _diagnostics;
    private readonly SyntaxListBuilder _leadingTriviaCache = new SyntaxListBuilder(10);
    private readonly SyntaxListBuilder _trailingTriviaCache = new SyntaxListBuilder(10);
    private readonly bool _allowPreprocessorDirectives;

    private LexerMode _mode;
    private DirectiveStack _directives;
    private int _position;
    private int _start;
    private SyntaxKind _kind;
    private object _value;

    /// <summary>
    /// Creates a new <see cref="Lexer" />, requires a fully initialized <see cref="SyntaxTree" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to lex from.</param>
    internal Lexer(SyntaxTree syntaxTree, bool allowPreprocessorDirectives) {
        this.syntaxTree = syntaxTree;
        _text = syntaxTree.text;
        _allowPreprocessorDirectives = allowPreprocessorDirectives;
        _diagnostics = new List<SyntaxDiagnostic>();
        _directives = DirectiveStack.Empty;
    }

    /// <summary>
    /// The <see cref="SyntaxTree"/> that is being lexed.
    /// </summary>
    internal SyntaxTree syntaxTree { get; }

    /// <summary>
    /// Current position of the lexer. This represents the next character that has not yet been lexed,
    /// not the most recently lexed character.
    /// </summary>
    internal int position => _position;

    /// <summary>
    /// All lexed preprocessor directives so far.
    /// </summary>
    internal DirectiveStack directives => _directives;

    private char _current => Peek(0);

    private char _lookahead => Peek(1);

    /// <summary>
    /// Lexes the next un-lexed text to create a single <see cref="SyntaxToken" />.
    /// </summary>
    /// <returns>A new <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken LexNext(LexerMode mode) {
        _mode = mode;
        var badTokens = new List<SyntaxToken>();
        SyntaxToken token;

        while (true) {
            token = _mode switch {
                LexerMode.Syntax => LexNextInternal(),
                LexerMode.Directive => LexDirectiveToken(),
                _ => throw ExceptionUtilities.Unreachable(),
            };

            if (token.kind == SyntaxKind.BadToken) {
                badTokens.Add(token);
                continue;
            }

            if (badTokens.Count > 0) {
                var leadingTrivia = new SyntaxListBuilder(token.leadingTrivia.Count + 10);

                foreach (var badToken in badTokens) {
                    leadingTrivia.AddRange(badToken.leadingTrivia);
                    var trivia = SyntaxFactory.SkippedTokensTrivia(badToken);
                    leadingTrivia.Add(trivia);
                    leadingTrivia.AddRange(badToken.trailingTrivia);
                }

                leadingTrivia.AddRange(token.leadingTrivia);
                token = token.TokenWithLeadingTrivia(leadingTrivia.ToListNode());
            }

            break;
        }

        return token;
    }

    /// <summary>
    /// Moves where the lexer is reading from.
    /// </summary>
    internal void Move(int position) {
        _position = position;
    }

    private static int GetFullWidth(SyntaxListBuilder builder) {
        var width = 0;

        for (var i = 0; i < builder.Count; i++)
            width += builder[i].fullWidth;

        return width;
    }

    private char Peek(int offset) {
        var index = _position + offset;

        if (index >= _text.length)
            return '\0';

        return _text[index];
    }

    private SyntaxToken LexNextInternal() {
        _leadingTriviaCache.Clear();
        ReadTrivia(_position > 0, false);

        var tokenPosition = _position;
        ReadToken();

        var tokenKind = _kind;
        var tokenValue = _value;
        var tokenWidth = _position - _start;
        var diagnostics = GetDiagnostics(GetFullWidth(_leadingTriviaCache));

        _trailingTriviaCache.Clear();
        ReadTrivia(true, true);

        var tokenText = SyntaxFacts.GetText(tokenKind) ?? _text.ToString(new TextSpan(tokenPosition, tokenWidth));

        return Create(tokenKind, tokenText, tokenValue, diagnostics);
    }

    private SyntaxToken LexDirectiveToken() {
        _start = _position;
        var tokenPosition = _position;
        ReadDirectiveToken();

        var tokenKind = _kind;
        var tokenWidth = _position - _start;
        var diagnostics = GetDiagnostics(0);

        _trailingTriviaCache.Clear();
        ReadDirectiveTrailingTrivia(tokenKind == SyntaxKind.EndOfDirectiveToken);

        var tokenText = _text.ToString(new TextSpan(tokenPosition, tokenWidth));

        return Create(tokenKind, tokenText, null, diagnostics);
    }

    private SyntaxDiagnostic[] GetDiagnostics(int leadingTriviaWidth) {
        if (leadingTriviaWidth > 0) {
            var array = new SyntaxDiagnostic[_diagnostics.Count];

            for (var i = 0; i < _diagnostics.Count; i++)
                array[i] = _diagnostics[i].WithOffset(_diagnostics[i].offset + leadingTriviaWidth);

            _diagnostics.Clear();
            return array;
        } else {
            var array = _diagnostics.ToArray();
            _diagnostics.Clear();
            return array;
        }
    }

    private void AddDiagnostic(Diagnostic diagnostic, int position, int width) {
        _diagnostics.Add(new SyntaxDiagnostic(diagnostic, position - _start, width));
    }

    private SyntaxToken Create(SyntaxKind kind, string text, object value, SyntaxDiagnostic[] diagnostics) {
        var leading = _leadingTriviaCache.ToListNode();
        var trailing = _trailingTriviaCache.ToListNode();

        var token = SyntaxFactory.Token(kind, text, value, leading, trailing, diagnostics);

        if (text is null)
            token.SetFlags(GreenNode.NodeFlags.IsMissing);

        return token;
    }

    private void ReadTrivia(bool afterFirstToken, bool isTrailing) {
        var triviaList = isTrailing ? ref _trailingTriviaCache : ref _leadingTriviaCache;
        var done = false;

        while (!done) {
            _start = _position;
            _kind = SyntaxKind.BadToken;
            _value = null;

            switch (_current) {
                case '\0':
                    done = true;
                    break;
                case '/':
                    if (_lookahead == '/')
                        ReadSingeLineComment();
                    else if (_lookahead == '*')
                        ReadMultiLineComment();
                    else
                        done = true;

                    break;
                case '\r':
                case '\n':
                    if (isTrailing)
                        done = true;

                    ReadLineBreak();
                    break;
                case '#':
                    if (_allowPreprocessorDirectives)
                        ReadDirective(afterFirstToken, isTrailing);
                    else
                        done = true;

                    break;
                case ' ':
                case '\t':
                    ReadWhitespace();
                    break;
                default:
                    // Other whitespace; use case labels on most common whitespace because its faster
                    if (char.IsWhiteSpace(_current))
                        ReadWhitespace();
                    else
                        done = true;

                    break;
            }

            var length = _position - _start;

            if (length > 0) {
                var text = _text.ToString(new TextSpan(_start, length));
                var trivia = SyntaxFactory.Trivia(_kind, text, GetDiagnostics(0));
                triviaList.Add(trivia);
            }
        }
    }

    private void ReadDirectiveTrivia(bool isTrailing) {
        var triviaList = isTrailing ? ref _trailingTriviaCache : ref _leadingTriviaCache; ;

        _start = _position;
        _kind = SyntaxKind.BadToken;
        _value = null;

        switch (_current) {
            case '/':
                if (_lookahead == '/')
                    ReadSingeLineComment();
                else if (_lookahead == '*')
                    ReadMultiLineComment();

                break;
            case '\r':
            case '\n':
                ReadLineBreak();
                break;
            case ' ':
            case '\t':
                ReadWhitespace();
                break;
            default:
                if (char.IsWhiteSpace(_current))
                    ReadWhitespace();

                break;
        }

        var length = _position - _start;

        if (length > 0) {
            var text = _text.ToString(new TextSpan(_start, length));
            var trivia = SyntaxFactory.Trivia(_kind, text, GetDiagnostics(0));
            triviaList.Add(trivia);
        }
    }

    private void ReadDirectiveToken() {
        switch (_current) {
            case '\0':
            case '\r':
            case '\n':
                _kind = SyntaxKind.EndOfDirectiveToken;
                break;
            case '#':
                _position++;
                _kind = SyntaxKind.HashToken;
                break;
            default:
                if (char.IsLetter(_current)) {
                    ReadIdentifierOrKeyword();
                } else {
                    AddDiagnostic(Error.BadCharacter(_current), _position, 1);
                    _position++;
                }

                break;
        }
    }

    private void ReadToken() {
        _start = _position;
        _kind = SyntaxKind.BadToken;
        _value = null;

        switch (_current) {
            case '\0':
                _kind = SyntaxKind.EndOfFileToken;
                break;
            case '.':
                _position++;
                _kind = SyntaxKind.PeriodToken;
                break;
            case ',':
                _position++;
                _kind = SyntaxKind.CommaToken;
                break;
            case '(':
                _position++;
                _kind = SyntaxKind.OpenParenToken;
                break;
            case ')':
                _position++;
                _kind = SyntaxKind.CloseParenToken;
                break;
            case '{':
                _position++;
                _kind = SyntaxKind.OpenBraceToken;
                break;
            case '}':
                _position++;
                _kind = SyntaxKind.CloseBraceToken;
                break;
            case '[':
                _position++;
                _kind = SyntaxKind.OpenBracketToken;
                break;
            case ']':
                _position++;
                _kind = SyntaxKind.CloseBracketToken;
                break;
            case ';':
                _position++;
                _kind = SyntaxKind.SemicolonToken;
                break;
            case ':':
                _position++;
                _kind = SyntaxKind.ColonToken;
                break;
            case '~':
                _position++;
                _kind = SyntaxKind.TildeToken;
                break;
            case '%':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.PercentEqualsToken;
                else _kind = SyntaxKind.PercentToken;
                break;
            case '^':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.CaretEqualsToken;
                else _kind = SyntaxKind.CaretToken;
                break;
            case '?':
                _position++;

                if (AdvanceIfMatches('?')) {
                    if (AdvanceIfMatches('=')) _kind = SyntaxKind.QuestionQuestionEqualsToken;
                    else _kind = SyntaxKind.QuestionQuestionToken;
                } else if (AdvanceIfMatches('.')) {
                    _kind = SyntaxKind.QuestionPeriodToken;
                } else if (AdvanceIfMatches('[')) {
                    _kind = SyntaxKind.QuestionOpenBracketToken;
                } else {
                    _kind = SyntaxKind.QuestionToken;
                }

                break;
            case '+':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.PlusEqualsToken;
                else if (AdvanceIfMatches('+')) _kind = SyntaxKind.PlusPlusToken;
                else _kind = SyntaxKind.PlusToken;
                break;
            case '-':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.MinusEqualsToken;
                else if (AdvanceIfMatches('-')) _kind = SyntaxKind.MinusMinusToken;
                else _kind = SyntaxKind.MinusToken;
                break;
            case '/':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.SlashEqualsToken;
                else _kind = SyntaxKind.SlashToken;
                break;
            case '*':
                _position++;

                if (AdvanceIfMatches('*')) {
                    if (AdvanceIfMatches('=')) _kind = SyntaxKind.AsteriskAsteriskEqualsToken;
                    else _kind = SyntaxKind.AsteriskAsteriskToken;
                } else if (AdvanceIfMatches('=')) {
                    _kind = SyntaxKind.AsteriskEqualsToken;
                } else {
                    _kind = SyntaxKind.AsteriskToken;
                }

                break;
            case '&':
                _position++;
                if (AdvanceIfMatches('&')) _kind = SyntaxKind.AmpersandAmpersandToken;
                else if (AdvanceIfMatches('=')) _kind = SyntaxKind.AmpersandEqualsToken;
                else _kind = SyntaxKind.AmpersandToken;
                break;
            case '|':
                _position++;
                if (AdvanceIfMatches('|')) _kind = SyntaxKind.PipePipeToken;
                else if (AdvanceIfMatches('=')) _kind = SyntaxKind.PipeEqualsToken;
                else _kind = SyntaxKind.PipeToken;
                break;
            case '=':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.EqualsEqualsToken;
                else _kind = SyntaxKind.EqualsToken;
                break;
            case '!':
                _position++;
                if (AdvanceIfMatches('=')) _kind = SyntaxKind.ExclamationEqualsToken;
                else _kind = SyntaxKind.ExclamationToken;
                break;
            case '<':
                _position++;

                if (AdvanceIfMatches('<')) {
                    if (AdvanceIfMatches('=')) _kind = SyntaxKind.LessThanLessThanEqualsToken;
                    else _kind = SyntaxKind.LessThanLessThanToken;
                } else if (AdvanceIfMatches('=')) {
                    _kind = SyntaxKind.LessThanEqualsToken;
                } else {
                    _kind = SyntaxKind.LessThanToken;
                }

                break;
            case '>':
                _position++;
                _kind = SyntaxKind.GreaterThanToken;

                if (AdvanceIfMatches('=')) {
                    _kind = SyntaxKind.GreaterThanEqualsToken;
                } else if (AdvanceIfMatches('>')) {
                    if (AdvanceIfMatches('=')) {
                        _kind = SyntaxKind.GreaterThanGreaterThanEqualsToken;
                    } else if (AdvanceIfMatches('>')) {
                        if (AdvanceIfMatches('=')) _kind = SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken;
                        else _position -= 2;
                    } else {
                        _position--;
                    }
                }

                break;
            case '"':
                ReadStringLiteral(false);
                break;
            case '\'':
                ReadStringLiteral(true);
                break;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                ReadNumericLiteral();
                break;
            case '_':
                ReadIdentifierOrKeyword();
                break;
            default:
                if (char.IsLetter(_current)) {
                    ReadIdentifierOrKeyword();
                } else {
                    AddDiagnostic(Error.BadCharacter(_current), _position, 1);
                    _position++;
                }

                break;
        }
    }

    private bool AdvanceIfMatches(char character) {
        if (_current == character) {
            _position++;
            return true;
        }

        return false;
    }

    private void ReadSingeLineComment() {
        _position += 2;
        var done = false;

        while (!done) {
            switch (_current) {
                case '\r':
                case '\n':
                case '\0':
                    done = true;
                    break;
                default:
                    _position++;
                    break;
            }
        }

        _kind = SyntaxKind.SingleLineCommentTrivia;
    }

    private void ReadMultiLineComment() {
        _position += 2;
        var done = false;

        while (!done) {
            switch (_current) {
                case '\0':
                    AddDiagnostic(Error.UnterminatedComment(), _start, 2);
                    done = true;
                    break;
                case '*':
                    if (_lookahead == '/') {
                        _position += 2;
                        done = true;
                    } else {
                        goto default;
                    }
                    break;
                default:
                    _position++;
                    break;
            }
        }

        _kind = SyntaxKind.MultiLineCommentTrivia;
    }

    private void ReadStringLiteral(bool isCharacter) {
        var saved = _position;
        _position++;
        var sb = new StringBuilder();
        var done = false;

        while (!done) {
            switch (_current) {
                case '\0':
                case '\r':
                case '\n':
                    AddDiagnostic(Error.UnterminatedString(), _start, 1);
                    done = true;
                    break;
                case '"' when !isCharacter:
                    if (_lookahead == '"') {
                        sb.Append(_current);
                        _position += 2;
                    } else {
                        _position++;
                        done = true;
                    }
                    break;
                case '\'' when isCharacter:
                    _position++;
                    done = true;
                    break;
                case '\\':
                    _position++;

                    switch (_current) {
                        case 'a':
                            sb.Append('\a');
                            _position++;
                            break;
                        case 'b':
                            sb.Append('\b');
                            _position++;
                            break;
                        case 'f':
                            sb.Append('\f');
                            _position++;
                            break;
                        case 'n':
                            sb.Append('\n');
                            _position++;
                            break;
                        case 'r':
                            sb.Append('\r');
                            _position++;
                            break;
                        case 't':
                            sb.Append('\t');
                            _position++;
                            break;
                        case 'v':
                            sb.Append('\v');
                            _position++;
                            break;
                        case '\'':
                            sb.Append('\'');
                            _position++;
                            break;
                        case '"':
                            sb.Append('"');
                            _position++;
                            break;
                        case '\\':
                            sb.Append('\\');
                            _position++;
                            break;
                        case '\0':
                            break;
                        default:
                            AddDiagnostic(Error.UnrecognizedEscapeSequence(_current), _position - 1, 2);
                            break;
                    }
                    break;
                default:
                    sb.Append(_current);
                    _position++;
                    break;
            }
        }

        _kind = isCharacter ? SyntaxKind.CharacterLiteralToken : SyntaxKind.StringLiteralToken;

        if (isCharacter) {
            if (isCharacter && sb.Length == 0) {
                AddDiagnostic(Error.EmptyCharacterLiteral(), saved, 2);
                _value = null;
            } else if (isCharacter && sb.Length > 1) {
                AddDiagnostic(Error.CharacterLiteralTooLong(), saved, sb.Length + 2);
                _value = sb[0];
            } else {
                _value = sb[0];
            }
        } else {
            _value = sb.ToString();
        }

    }

    private void ReadNumericLiteral() {
        var hasDecimal = false;
        var hasExponent = false;
        var isBinary = false;
        var isHexadecimal = false;
        char? previous = null;

        bool IsValidCharacter(char c) {
            if (isBinary && c == '0' || c == '1') {
                return true;
            } else if (isHexadecimal && char.IsAsciiHexDigit(c)) {
                return true;
            } else if (!isBinary && !isHexadecimal && char.IsDigit(c)) {
                return true;
            } else {
                return false;
            }
        }

        if (_current == '0') {
            if (char.ToLower(_lookahead) == 'b') {
                isBinary = true;
                _position += 2;
            } else if (char.ToLower(_lookahead) == 'x') {
                isHexadecimal = true;
                _position += 2;
            }
        }

        while (true) {
            if (_current == '.' && !isBinary && !isHexadecimal && !hasDecimal && !hasExponent) {
                hasDecimal = true;
                _position++;
            } else if (char.ToLower(_current) == 'e' && !isBinary && !isHexadecimal && !hasExponent &&
                (((_lookahead == '-' || _lookahead == '+') &&
                IsValidCharacter(Peek(2))) || IsValidCharacter(_lookahead))) {
                hasExponent = true;
                _position++;
            } else if ((_current == '-' || _current == '+') && char.ToLower(previous.Value) == 'e') {
                _position++;
            } else if (_current == '_' && previous.HasValue && IsValidCharacter(_lookahead)) {
                _position++;
            } else if (IsValidCharacter(_current)) {
                _position++;
            } else {
                break;
            }

            previous = Peek(-1);
        }

        var length = _position - _start;
        var text = _text.ToString(new TextSpan(_start, length));
        var parsedText = text.Replace("_", "");

        if (!hasDecimal && !hasExponent) {
            var @base = isBinary ? 2 : 16;
            var failed = false;
            var value = 0;

            if (isBinary || isHexadecimal) {
                try {
                    value = Convert.ToInt32(
                        text.Length > 2 ? parsedText.Substring(2) : throw new FormatException(), @base);
                } catch (Exception e) when (e is OverflowException || e is FormatException) {
                    failed = true;
                }
            } else if (!int.TryParse(parsedText, out value)) {
                failed = true;
            }

            if (failed)
                AddDiagnostic(Error.InvalidType(text, TypeSymbol.Int), _start, length);
            else
                _value = value;
        } else {
            if (!double.TryParse(parsedText, out var value))
                AddDiagnostic(Error.InvalidType(text, TypeSymbol.Decimal), _start, length);
            else
                _value = value;
        }

        _kind = SyntaxKind.NumericLiteralToken;
    }

    private void ReadWhitespace() {
        var done = false;

        while (!done) {
            switch (_current) {
                case '\0':
                case '\r':
                case '\n':
                    done = true;
                    break;
                default:
                    if (!char.IsWhiteSpace(_current))
                        done = true;
                    else
                        _position++;
                    break;
            }
        }

        _kind = SyntaxKind.WhitespaceTrivia;
    }

    private void ReadLineBreak() {
        if (_current == '\r' && _lookahead == '\n')
            _position += 2;
        else
            _position++;

        _kind = SyntaxKind.EndOfLineTrivia;
    }

    private void ReadDirective(bool afterFirstToken, bool afterNonWhitespaceOnLine) {
        var saveMode = _mode;
        var directiveParser = new DirectiveParser(this, _directives);
        var directive = directiveParser.ParseDirective(afterFirstToken, afterNonWhitespaceOnLine);

        var triviaList = afterNonWhitespaceOnLine ? _trailingTriviaCache : _leadingTriviaCache;
        triviaList.Add(directive);

        _directives = directive.ApplyDirectives(_directives);
        _mode = saveMode;
    }

    private void ReadDirectiveTrailingTrivia(bool includeEndOfLine) {
        while (true) {
            var position = _position;
            ReadDirectiveTrivia(true);

            if (_position == position) {
                break;
            } else if (_kind == SyntaxKind.EndOfLineTrivia) {
                if (!includeEndOfLine) {
                    _trailingTriviaCache.RemoveLast();
                    _position = position;
                }

                break;
            }
        }
    }

    private void ReadIdentifierOrKeyword() {
        while (char.IsLetterOrDigit(_current) || _current == '_')
            _position++;

        var length = _position - _start;
        var text = _text.ToString(new TextSpan(_start, length));
        _kind = SyntaxFacts.GetKeywordType(text);
    }
}
