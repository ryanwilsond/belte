using System;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Converts source text into parsable SyntaxTokens.<br/>
/// E.g.
/// <code>
/// int myInt;
/// --->
/// IdentiferToken IdentifierToken SemicolonToken
/// </code>
/// </summary>
internal sealed class Lexer {
    internal char current => Peek(0);
    internal char lookahead => Peek(1);

    private readonly SourceText _text;
    private int _position;
    private int _start;
    private SyntaxKind _kind;
    private object _value;
    private SyntaxTree _syntaxTree;
    private ImmutableArray<SyntaxTrivia>.Builder _triviaBuilder = ImmutableArray.CreateBuilder<SyntaxTrivia>();

    /// <summary>
    /// Creates a new <see cref="Lexer" />, requires a fully initialized <see cref="SyntaxTree" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> to lex from.</param>
    internal Lexer(SyntaxTree syntaxTree) {
        _text = syntaxTree.text;
        _syntaxTree = syntaxTree;
        diagnostics = new BelteDiagnosticQueue();
    }

    /// <summary>
    /// Diagnostics produced during lexing process
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Current position of the lexer. This represents the next character that has not yet been lexed,
    /// not the most recently lexed character.
    /// </summary>
    internal int position => _position;

    /// <summary>
    /// Lexes the next un-lexed text to create a single <see cref="SyntaxToken" />.
    /// </summary>
    /// <returns>A new <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken LexNext() {
        ReadTrivia(true);
        var leadingTrivia = _triviaBuilder.ToImmutable();
        var tokenStart = _position;

        ReadToken();

        var tokenKind = _kind;
        var tokenValue = _value;
        var tokenLength = _position - _start;

        ReadTrivia(false);
        var trailingTrivia = _triviaBuilder.ToImmutable();

        var tokenText = SyntaxFacts.GetText(tokenKind) ?? _text.ToString(new TextSpan(tokenStart, tokenLength));

        return new SyntaxToken(
            _syntaxTree, tokenKind, tokenStart, tokenText, tokenValue, leadingTrivia, trailingTrivia
        );
    }

    /// <summary>
    /// Moves where the lexer is reading from.
    /// </summary>
    internal void Move(int position) {
        _position = position;
    }

    private char Peek(int offset) {
        var index = _position + offset;

        if (index >= _text.length)
            return '\0';

        return _text[index];
    }

    private void ReadTrivia(bool leading) {
        _triviaBuilder.Clear();
        var done = false;

        while (!done) {
            _start = _position;
            _kind = SyntaxKind.BadToken;
            _value = null;

            switch (current) {
                case '\0':
                    done = true;
                    break;
                case '/':
                    if (lookahead == '/')
                        ReadSingeLineComment();
                    else if (lookahead == '*')
                        ReadMultiLineComment();
                    else
                        done = true;
                    break;
                case '\r':
                case '\n':
                    if (!leading)
                        done = true;
                    ReadLineBreak();
                    break;
                case ' ':
                case '\t':
                    ReadWhitespace();
                    break;
                default:
                    // Other whitespace; use case labels on most common whitespace because its faster
                    if (char.IsWhiteSpace(current))
                        ReadWhitespace();
                    else
                        done = true;
                    break;
            }

            var length = _position - _start;

            if (length > 0) {
                var text = _text.ToString(new TextSpan(_start, length));
                var trivia = new SyntaxTrivia(_syntaxTree, _kind, _start, text);
                _triviaBuilder.Add(trivia);
            }
        }
    }

    private void ReadToken() {
        _start = _position;
        _kind = SyntaxKind.BadToken;
        _value = null;

        switch (current) {
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
                if (current == '=') {
                    _kind = SyntaxKind.PercentEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.PercentToken;
                }
                break;
            case '^':
                _position++;
                if (current == '=') {
                    _kind = SyntaxKind.CaretEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.CaretToken;
                }
                break;
            case '?':
                if (lookahead == '?') {
                    _position += 2;
                    if (current == '=') {
                        _kind = SyntaxKind.QuestionQuestionEqualsToken;
                        _position++;
                    } else {
                        _kind = SyntaxKind.QuestionQuestionToken;
                    }
                } else if (lookahead == '.') {
                    _kind = SyntaxKind.QuestionPeriodToken;
                    _position += 2;
                } else if (lookahead == '[') {
                    _kind = SyntaxKind.QuestionOpenBracketToken;
                    _position += 2;
                } else {
                    _kind = SyntaxKind.QuestionToken;
                    _position++;
                }
                break;
            case '+':
                _position++;
                if (current == '=') {
                    _kind = SyntaxKind.PlusEqualsToken;
                    _position++;
                } else if (current == '+') {
                    _kind = SyntaxKind.PlusPlusToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.PlusToken;
                }
                break;
            case '-':
                _position++;
                if (current == '=') {
                    _kind = SyntaxKind.MinusEqualsToken;
                    _position++;
                } else if (current == '-') {
                    _kind = SyntaxKind.MinusMinusToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.MinusToken;
                }
                break;
            case '/':
                _position++;
                if (current == '=') {
                    _kind = SyntaxKind.SlashEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.SlashToken;
                }
                break;
            case '*':
                _position++;
                if (current == '*') {
                    if (lookahead == '=') {
                        _position++;
                        _kind = SyntaxKind.AsteriskAsteriskEqualsToken;
                    } else {
                        _kind = SyntaxKind.AsteriskAsteriskToken;
                    }
                    _position++;
                } else if (current == '=') {
                    _kind = SyntaxKind.AsteriskEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.AsteriskToken;
                }
                break;
            case '&':
                _position++;
                if (current == '&') {
                    _kind = SyntaxKind.AmpersandAmpersandToken;
                    _position++;
                } else if (current == '=') {
                    _kind = SyntaxKind.AmpersandEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.AmpersandToken;
                }
                break;
            case '|':
                _position++;
                if (current == '|') {
                    _kind = SyntaxKind.PipePipeToken;
                    _position++;
                } else if (current == '=') {
                    _kind = SyntaxKind.PipeEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.PipeToken;
                }
                break;
            case '=':
                _position++;
                if (current == '=') {
                    _kind = SyntaxKind.EqualsEqualsToken;
                    _position++;
                } else {
                    _kind = SyntaxKind.EqualsToken;
                }
                break;
            case '!':
                _position++;
                if (current == '=') {
                    _position++;
                    _kind = SyntaxKind.ExclamationEqualsToken;
                } else {
                    _kind = SyntaxKind.ExclamationToken;
                }
                break;
            case '<':
                _position++;
                if (current == '=') {
                    _position++;
                    _kind = SyntaxKind.LessThanEqualsToken;
                } else if (current == '<') {
                    if (lookahead == '=') {
                        _position++;
                        _kind = SyntaxKind.LessThanLessThanEqualsToken;
                    } else {
                        _kind = SyntaxKind.LessThanLessThanToken;
                    }
                    _position++;
                } else {
                    _kind = SyntaxKind.LessThanToken;
                }
                break;
            case '>':
                _position++;
                if (current == '=') {
                    _position++;
                    _kind = SyntaxKind.GreaterThanEqualsToken;
                } else if (current == '>') {
                    if (lookahead == '=') {
                        _position++;
                        _kind = SyntaxKind.GreaterThanGreaterThanEqualsToken;
                    } else if (lookahead == '>') {
                        if (Peek(2) == '=') {
                            _position++;
                            _kind = SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken;
                        } else {
                            _kind = SyntaxKind.GreaterThanGreaterThanGreaterThanToken;
                        }
                        _position++;
                    } else {
                        _kind = SyntaxKind.GreaterThanGreaterThanToken;
                    }
                    _position++;
                } else {
                    _kind = SyntaxKind.GreaterThanToken;
                }
                break;
            case '"':
                ReadStringLiteral();
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
                if (char.IsLetter(current)) {
                    ReadIdentifierOrKeyword();
                } else {
                    var span = new TextSpan(_position, 1);
                    var location = new TextLocation(_text, span);
                    diagnostics.Push(Error.BadCharacter(location, _position, current));
                    _position++;
                }

                break;
        }
    }

    private void ReadSingeLineComment() {
        _position += 2;
        var done = false;

        while (!done) {
            switch (current) {
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
            switch (current) {
                case '\0':
                    var span = new TextSpan(_start, 2);
                    var location = new TextLocation(_text, span);
                    diagnostics.Push(Error.UnterminatedComment(location));
                    done = true;
                    break;
                case '*':
                    if (lookahead == '/') {
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

    private void ReadStringLiteral() {
        _position++;
        var sb = new StringBuilder();
        var done = false;

        while (!done) {
            switch (current) {
                case '\0':
                case '\r':
                case '\n':
                    var span = new TextSpan(_start, 1);
                    var location = new TextLocation(_text, span);
                    diagnostics.Push(Error.UnterminatedString(location));
                    done = true;
                    break;
                case '"':
                    if (lookahead == '"') {
                        sb.Append(current);
                        _position += 2;
                    } else {
                        _position++;
                        done = true;
                    }
                    break;
                case '\\':
                    _position++;

                    switch (current) {
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
                            var errorSpan = new TextSpan(_position - 1, 2);
                            var errorLocation = new TextLocation(_text, errorSpan);
                            diagnostics.Push(Error.UnrecognizedEscapeSequence(errorLocation, current));
                            break;
                    }
                    break;
                default:
                    sb.Append(current);
                    _position++;
                    break;
            }
        }

        _kind = SyntaxKind.StringLiteralToken;
        _value = sb.ToString();
    }

    private void ReadNumericLiteral() {
        var hasDecimal = false;
        var hasExponent = false;
        var isBinary = false;
        var isHexadecimal = false;
        char? previous = null;

        bool isValidCharacter(char c) {
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

        if (current == '0') {
            if (char.ToLower(lookahead) == 'b') {
                isBinary = true;
                _position += 2;
            } else if (char.ToLower(lookahead) == 'x') {
                isHexadecimal = true;
                _position += 2;
            }
        }

        while (true) {
            if (current == '.' && !isBinary && !isHexadecimal && !hasDecimal && !hasExponent) {
                hasDecimal = true;
                _position++;
            } else if (char.ToLower(current) == 'e' && !isBinary && !isHexadecimal && !hasExponent &&
                (((lookahead == '-' || lookahead == '+') &&
                isValidCharacter(Peek(2))) || isValidCharacter(lookahead))) {
                hasExponent = true;
                _position++;
            } else if ((current == '-' || current == '+') && char.ToLower(previous.Value) == 'e') {
                _position++;
            } else if (current == '_' && previous.HasValue && isValidCharacter(lookahead)) {
                _position++;
            } else if (isValidCharacter(current)) {
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

            if (failed) {
                var span = new TextSpan(_start, length);
                var location = new TextLocation(_text, span);
                diagnostics.Push(Error.InvalidType(location, text, TypeSymbol.Int));
            } else {
                _value = value;
            }
        } else {
            if (!double.TryParse(parsedText, out var value)) {
                var span = new TextSpan(_start, length);
                var location = new TextLocation(_text, span);
                diagnostics.Push(Error.InvalidType(location, text, TypeSymbol.Decimal));
            } else {
                _value = value;
            }
        }

        _kind = SyntaxKind.NumericLiteralToken;
    }

    private void ReadWhitespace() {
        var done = false;

        while (!done) {
            switch (current) {
                case '\0':
                case '\r':
                case '\n':
                    done = true;
                    break;
                default:
                    if (!char.IsWhiteSpace(current))
                        done = true;
                    else
                        _position++;
                    break;
            }
        }

        _kind = SyntaxKind.WhitespaceTrivia;
    }

    private void ReadLineBreak() {
        if (current == '\r' && lookahead == '\n')
            _position += 2;
        else
            _position++;

        _kind = SyntaxKind.EndOfLineTrivia;
    }

    private void ReadIdentifierOrKeyword() {
        while (char.IsLetterOrDigit(current) || current == '_')
            _position++;

        var length = _position - _start;
        var text = _text.ToString(new TextSpan(_start, length));
        _kind = SyntaxFacts.GetKeywordType(text);
    }
}
