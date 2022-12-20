using System.Text;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Symbols;
using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Converts source text into parsable Tokens.<br/>
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
    private SyntaxType _type;
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
    /// Lexes the next un-lexed text to create a single <see cref="Token" />.
    /// </summary>
    /// <returns>A new <see cref="Token" />.</returns>
    internal Token LexNext() {
        ReadTrivia(true);
        var leadingTrivia = _triviaBuilder.ToImmutable();
        var tokenStart = _position;

        ReadToken();

        var tokenType = _type;
        var tokenValue = _value;
        var tokenLength = _position - _start;

        ReadTrivia(false);
        var trailingTrivia = _triviaBuilder.ToImmutable();

        var tokenText = SyntaxFacts.GetText(tokenType);
        if (tokenText == null)
            tokenText = _text.ToString(tokenStart, tokenLength);

        return new Token(_syntaxTree, tokenType, tokenStart, tokenText, tokenValue, leadingTrivia, trailingTrivia);
    }

    private char Peek(int offset) {
        int index = _position + offset;

        if (index >= _text.length)
            return '\0';

        return _text[index];
    }

    private void ReadTrivia(bool leading) {
        _triviaBuilder.Clear();
        var done = false;

        while (!done) {
            _start = _position;
            _type = SyntaxType.BadToken;
            _value = null;

            switch (current) {
                case '\0':
                    done = true;
                    break;
                case '/':
                    if (lookahead == '/')
                        // TODO Docstring comments (xml/doxygen, probably xml)
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
                    // ! However the speed gain is almost definitely negligible and probably not worth the
                    // ! readability loss
                    if (char.IsWhiteSpace(current))
                        ReadWhitespace();
                    else
                        done = true;
                    break;
            }

            var length = _position - _start;

            if (length > 0) {
                var text = _text.ToString(_start, length);
                var trivia = new SyntaxTrivia(_syntaxTree, _type, _start, text);
                _triviaBuilder.Add(trivia);
            }
        }
    }

    private void ReadToken() {
        _start = _position;
        _type = SyntaxType.BadToken;
        _value = null;

        switch (current) {
            case '\0':
                _type = SyntaxType.EndOfFileToken;
                break;
            case '.':
                _position++;
                _type = SyntaxType.PeriodToken;
                break;
            case ',':
                _position++;
                _type = SyntaxType.CommaToken;
                break;
            case '(':
                _position++;
                _type = SyntaxType.OpenParenToken;
                break;
            case ')':
                _position++;
                _type = SyntaxType.CloseParenToken;
                break;
            case '{':
                _position++;
                _type = SyntaxType.OpenBraceToken;
                break;
            case '}':
                _position++;
                _type = SyntaxType.CloseBraceToken;
                break;
            case '[':
                _position++;
                _type = SyntaxType.OpenBracketToken;
                break;
            case ']':
                _position++;
                _type = SyntaxType.CloseBracketToken;
                break;
            case ';':
                _position++;
                _type = SyntaxType.SemicolonToken;
                break;
            case ':':
                _position++;
                _type = SyntaxType.ColonToken;
                break;
            case '~':
                _position++;
                _type = SyntaxType.TildeToken;
                break;
            case '%':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.PercentEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.PercentToken;
                }
                break;
            case '^':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.CaretEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.CaretToken;
                }
                break;
            case '?':
                if (lookahead == '?') {
                    _position += 2;
                    if (current == '=') {
                        _type = SyntaxType.QuestionQuestionEqualsToken;
                        _position++;
                    } else {
                        _type = SyntaxType.QuestionQuestionToken;
                    }
                } else {
                    _type = SyntaxType.QuestionToken;
                    _position++;
                }
                break;
            case '+':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.PlusEqualsToken;
                    _position++;
                } else if (current == '+') {
                    _type = SyntaxType.PlusPlusToken;
                    _position++;
                } else {
                    _type = SyntaxType.PlusToken;
                }
                break;
            case '-':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.MinusEqualsToken;
                    _position++;
                } else if (current == '-') {
                    _type = SyntaxType.MinusMinusToken;
                    _position++;
                } else {
                    _type = SyntaxType.MinusToken;
                }
                break;
            case '/':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.SlashEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.SlashToken;
                }
                break;
            case '*':
                _position++;
                if (current == '*') {
                    if (lookahead == '=') {
                        _position++;
                        _type = SyntaxType.AsteriskAsteriskEqualsToken;
                    } else {
                        _type = SyntaxType.AsteriskAsteriskToken;
                    }
                    _position++;
                } else if (current == '=') {
                    _type = SyntaxType.AsteriskEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.AsteriskToken;
                }
                break;
            case '&':
                _position++;
                if (current == '&') {
                    _type = SyntaxType.AmpersandAmpersandToken;
                    _position++;
                } else if (current == '=') {
                    _type = SyntaxType.AmpersandEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.AmpersandToken;
                }
                break;
            case '|':
                _position++;
                if (current == '|') {
                    _type = SyntaxType.PipePipeToken;
                    _position++;
                } else if (current == '=') {
                    _type = SyntaxType.PipeEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.PipeToken;
                }
                break;
            case '=':
                _position++;
                if (current == '=') {
                    _type = SyntaxType.EqualsEqualsToken;
                    _position++;
                } else {
                    _type = SyntaxType.EqualsToken;
                }
                break;
            case '!':
                _position++;
                if (current == '=') {
                    _position++;
                    _type = SyntaxType.ExclamationEqualsToken;
                } else {
                    _type = SyntaxType.ExclamationToken;
                }
                break;
            case '<':
                _position++;
                if (current == '=') {
                    _position++;
                    _type = SyntaxType.LessThanEqualsToken;
                } else if (current == '<') {
                    if (lookahead == '=') {
                        _position++;
                        _type = SyntaxType.LessThanLessThanEqualsToken;
                    } else {
                        _type = SyntaxType.LessThanLessThanToken;
                    }
                    _position++;
                } else {
                    _type = SyntaxType.LessThanToken;
                }
                break;
            case '>':
                _position++;
                if (current == '=') {
                    _position++;
                    _type = SyntaxType.GreaterThanEqualsToken;
                } else if (current == '>') {
                    if (lookahead == '=') {
                        _position++;
                        _type = SyntaxType.GreaterThanGreaterThanEqualsToken;
                    } else if (lookahead == '>') {
                        if (Peek(2) == '=') {
                            _position++;
                            _type = SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken;
                        } else {
                            _type = SyntaxType.GreaterThanGreaterThanGreaterThanToken;
                        }
                        _position++;
                    } else {
                        _type = SyntaxType.GreaterThanGreaterThanToken;
                    }
                    _position++;
                } else {
                    _type = SyntaxType.GreaterThanToken;
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
                if (char.IsLetter(current))
                    ReadIdentifierOrKeyword();
                else {
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

        _type = SyntaxType.SingleLineCommentTrivia;
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

        _type = SyntaxType.MultiLineCommentTrivia;
    }

    private void ReadStringLiteral() {
        _position++;
        var sb = new StringBuilder();
        bool done = false;

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
                        default:
                            sb.Append('\\');
                            break;
                    }
                    break;
                default:
                    sb.Append(current);
                    _position++;
                    break;
            }
        }

        _type = SyntaxType.StringLiteralToken;
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

        int length = _position - _start;
        string text = _text.ToString(_start, length);
        string parsedText = text.Replace("_", "");

        if (!hasDecimal && !hasExponent) {
            var @base = isBinary ? 2 : 16;
            var failed = false;
            int value = 0;

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
                diagnostics.Push(Error.InvalidType(location, text, TypeSymbol.Int));
            } else {
                _value = value;
            }
        }

        _type = SyntaxType.NumericLiteralToken;
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

        _type = SyntaxType.WhitespaceTrivia;
    }

    private void ReadLineBreak() {
        if (current == '\r' && lookahead == '\n')
            _position += 2;
        else
            _position++;

        _type = SyntaxType.EndOfLineTrivia;
    }

    private void ReadIdentifierOrKeyword() {
        while (char.IsLetterOrDigit(current) || current == '_')
            _position++;

        int length = _position - _start;
        string text = _text.ToString(_start, length);
        _type = SyntaxFacts.GetKeywordType(text);
    }
}
