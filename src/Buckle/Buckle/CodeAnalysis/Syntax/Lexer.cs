using System.Text;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Symbols;
using System;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Converts source text into parsable tokens.
/// E.g. int myInt; -> IdentiferToken IdentifierToken SemicolonToken
/// </summary>
internal sealed class Lexer {
    private readonly SourceText text_;
    private int position_;
    private int start_;
    private SyntaxType type_;
    private object value_;
    private SyntaxTree syntaxTree_;
    private char current => Peek(0);
    private char lookahead => Peek(1);
    private ImmutableArray<SyntaxTrivia>.Builder triviaBuilder_ = ImmutableArray.CreateBuilder<SyntaxTrivia>();

    /// <summary>
    /// Creates a new lexer, requires a fully initialized syntax tree.
    /// </summary>
    /// <param name="syntaxTree">Syntax tree to lex from</param>
    internal Lexer(SyntaxTree syntaxTree) {
        text_ = syntaxTree.text;
        syntaxTree_ = syntaxTree;
        diagnostics = new BelteDiagnosticQueue();
    }

    /// <summary>
    /// Diagnostics produced during lexing process
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Lexes the next un-lexed text to create a single token.
    /// </summary>
    /// <returns>A new token</returns>
    internal Token LexNext() {
        ReadTrivia(true);
        var leadingTrivia = triviaBuilder_.ToImmutable();
        var tokenStart = position_;

        ReadToken();

        var tokenType = type_;
        var tokenValue = value_;
        var tokenLength = position_ - start_;

        ReadTrivia(false);
        var trailingTrivia = triviaBuilder_.ToImmutable();

        var tokenText = SyntaxFacts.GetText(tokenType);
        if (tokenText == null)
            tokenText = text_.ToString(tokenStart, tokenLength);

        return new Token(syntaxTree_, tokenType, tokenStart, tokenText, tokenValue, leadingTrivia, trailingTrivia);
    }

    private char Peek(int offset) {
        int index = position_ + offset;

        if (index >= text_.length)
            return '\0';

        return text_[index];
    }

    private void ReadTrivia(bool leading) {
        triviaBuilder_.Clear();
        var done = false;

        while (!done) {
            start_ = position_;
            type_ = SyntaxType.BAD_TOKEN;
            value_ = null;

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
                    // ! However the speed gain is almost definitely negligible and probably not worth the readability loss
                    if (char.IsWhiteSpace(current))
                        ReadWhitespace();
                    else
                        done = true;
                    break;
            }

            var length = position_ - start_;

            if (length > 0) {
                var text = text_.ToString(start_, length);
                var trivia = new SyntaxTrivia(syntaxTree_, type_, start_, text);
                triviaBuilder_.Add(trivia);
            }
        }
    }

    private void ReadToken() {
        start_ = position_;
        type_ = SyntaxType.BAD_TOKEN;
        value_ = null;

        switch (current) {
            case '\0':
                type_ = SyntaxType.END_OF_FILE_TOKEN;
                break;
            case ',':
                position_++;
                type_ = SyntaxType.COMMA_TOKEN;
                break;
            case '(':
                position_++;
                type_ = SyntaxType.OPEN_PAREN_TOKEN;
                break;
            case ')':
                position_++;
                type_ = SyntaxType.CLOSE_PAREN_TOKEN;
                break;
            case '{':
                position_++;
                type_ = SyntaxType.OPEN_BRACE_TOKEN;
                break;
            case '}':
                position_++;
                type_ = SyntaxType.CLOSE_BRACE_TOKEN;
                break;
            case '[':
                position_++;
                type_ = SyntaxType.OPEN_BRACKET_TOKEN;
                break;
            case ']':
                position_++;
                type_ = SyntaxType.CLOSE_BRACKET_TOKEN;
                break;
            case ';':
                position_++;
                type_ = SyntaxType.SEMICOLON_TOKEN;
                break;
            case '~':
                position_++;
                type_ = SyntaxType.TILDE_TOKEN;
                break;
            case '%':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.PERCENT_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.PERCENT_TOKEN;
                }
                break;
            case '^':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.CARET_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.CARET_TOKEN;
                }
                break;
            case '+':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.PLUS_EQUALS_TOKEN;
                    position_++;
                } else if (current == '+') {
                    type_ = SyntaxType.PLUS_PLUS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.PLUS_TOKEN;
                }
                break;
            case '-':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.MINUS_EQUALS_TOKEN;
                    position_++;
                } else if (current == '-') {
                    type_ = SyntaxType.MINUS_MINUS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.MINUS_TOKEN;
                }
                break;
            case '/':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.SLASH_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.SLASH_TOKEN;
                }
                break;
            case '*':
                position_++;
                if (current == '*') {
                    if (lookahead == '=') {
                        position_++;
                        type_ = SyntaxType.ASTERISK_ASTERISK_EQUALS_TOKEN;
                    } else {
                        type_ = SyntaxType.ASTERISK_ASTERISK_TOKEN;
                    }
                    position_++;
                } else if (current == '=') {
                    type_ = SyntaxType.ASTERISK_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.ASTERISK_TOKEN;
                }
                break;
            case '&':
                position_++;
                if (current == '&') {
                    type_ = SyntaxType.AMPERSAND_AMPERSAND_TOKEN;
                    position_++;
                } else if (current == '=') {
                    type_ = SyntaxType.AMPERSAND_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.AMPERSAND_TOKEN;
                }
                break;
            case '|':
                position_++;
                if (current == '|') {
                    type_ = SyntaxType.PIPE_PIPE_TOKEN;
                    position_++;
                } else if (current == '=') {
                    type_ = SyntaxType.PIPE_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.PIPE_TOKEN;
                }
                break;
            case '=':
                position_++;
                if (current == '=') {
                    type_ = SyntaxType.EQUALS_EQUALS_TOKEN;
                    position_++;
                } else {
                    type_ = SyntaxType.EQUALS_TOKEN;
                }
                break;
            case '!':
                position_++;
                if (current == '=') {
                    position_++;
                    type_ = SyntaxType.EXCLAMATION_EQUALS_TOKEN;
                } else {
                    type_ = SyntaxType.EXCLAMATION_TOKEN;
                }
                break;
            case '<':
                position_++;
                if (current == '=') {
                    position_++;
                    type_ = SyntaxType.LESS_THAN_EQUALS_TOKEN;
                } else if (current == '<') {
                    if (lookahead == '=') {
                        position_++;
                        type_ = SyntaxType.LESS_THAN_LESS_THAN_EQUALS_TOKEN;
                    } else {
                        type_ = SyntaxType.LESS_THAN_LESS_THAN_TOKEN;
                    }
                    position_++;
                } else {
                    type_ = SyntaxType.LESS_THAN_TOKEN;
                }
                break;
            case '>':
                position_++;
                if (current == '=') {
                    position_++;
                    type_ = SyntaxType.GREATER_THAN_EQUALS_TOKEN;
                } else if (current == '>') {
                    if (lookahead == '=') {
                        position_++;
                        type_ = SyntaxType.GREATER_THAN_GREATER_THAN_EQUALS_TOKEN;
                    } else if (lookahead == '>') {
                        if (Peek(2) == '=') {
                            position_++;
                            type_ = SyntaxType.GREATER_THAN_GREATER_THAN_GREATER_THAN_EQUALS_TOKEN;
                        } else {
                            type_ = SyntaxType.GREATER_THAN_GREATER_THAN_GREATER_THAN_TOKEN;
                        }
                        position_++;
                    } else {
                        type_ = SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN;
                    }
                    position_++;
                } else {
                    type_ = SyntaxType.GREATER_THAN_TOKEN;
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
                    var span = new TextSpan(position_, 1);
                    var location = new TextLocation(text_, span);
                    diagnostics.Push(Error.BadCharacter(location, position_, current));
                    position_++;
                }
                break;
        }
    }

    private void ReadSingeLineComment() {
        position_ += 2;
        var done = false;

        while (!done) {
            switch (current) {
                case '\r':
                case '\n':
                case '\0':
                    done = true;
                    break;
                default:
                    position_++;
                    break;
            }
        }

        type_ = SyntaxType.SINGLELINE_COMMENT_TRIVIA;
    }

    private void ReadMultiLineComment() {
        position_ += 2;
        var done = false;

        while (!done) {
            switch (current) {
                case '\0':
                    var span = new TextSpan(start_, 2);
                    var location = new TextLocation(text_, span);
                    diagnostics.Push(Error.UnterminatedComment(location));
                    done = true;
                    break;
                case '*':
                    if (lookahead == '/') {
                        position_ += 2;
                        done = true;
                    } else {
                        goto default;
                    }
                    break;
                default:
                    position_++;
                    break;
            }
        }

        type_ = SyntaxType.MULTILINE_COMMENT_TRIVIA;
    }

    private void ReadStringLiteral() {
        position_++;
        var sb = new StringBuilder();
        bool done = false;

        while (!done) {
            switch (current) {
                case '\0':
                case '\r':
                case '\n':
                    var span = new TextSpan(start_, 1);
                    var location = new TextLocation(text_, span);
                    diagnostics.Push(Error.UnterminatedString(location));
                    done = true;
                    break;
                case '"':
                    if (lookahead == '"') {
                        sb.Append(current);
                        position_ += 2;
                    } else {
                        position_++;
                        done = true;
                    }
                    break;
                case '\\':
                    position_++;

                    switch (current) {
                        case 'a':
                            sb.Append('\a');
                            position_++;
                            break;
                        case 'b':
                            sb.Append('\b');
                            position_++;
                            break;
                        case 'f':
                            sb.Append('\f');
                            position_++;
                            break;
                        case 'n':
                            sb.Append('\n');
                            position_++;
                            break;
                        case 'r':
                            sb.Append('\r');
                            position_++;
                            break;
                        case 't':
                            sb.Append('\t');
                            position_++;
                            break;
                        case 'v':
                            sb.Append('\v');
                            position_++;
                            break;
                        case '\'':
                            sb.Append('\'');
                            position_++;
                            break;
                        case '"':
                            sb.Append('"');
                            position_++;
                            break;
                        case '\\':
                            sb.Append('\\');
                            position_++;
                            break;
                        default:
                            sb.Append('\\');
                            break;
                    }
                    break;
                default:
                    sb.Append(current);
                    position_++;
                    break;
            }
        }

        type_ = SyntaxType.STRING_LITERAL_TOKEN;
        value_ = sb.ToString();
    }

    private void ReadNumericLiteral() {
        var done = false;
        var hasDecimal = false;
        var isBinary = false;
        var isHexadecimal = false;

        if (current == '0') {
            if (lookahead == 'b' || lookahead == 'B') {
                isBinary = true;
                position_ += 2;
            } else if (lookahead == 'x' || lookahead == 'X') {
                isHexadecimal = true;
                position_ += 2;
            }
        }

        while (!done) {
            if (!isBinary && !isHexadecimal && !hasDecimal && current == '.') {
                hasDecimal = true;
                position_++;
                continue;
            }

            if (isBinary && current == '0' || current == '1') {
                position_++;
            } else if (isHexadecimal && char.IsAsciiHexDigit(current)) {
                position_++;
            } else if (!isBinary && !isHexadecimal && char.IsDigit(current)) {
                position_++;
            } else {
                done = true;
            }
        }

        int length = position_ - start_;
        string text = text_.ToString(start_, length);

        if (!hasDecimal) {
            var @base = isBinary ? 2 : 16;
            var failed = false;
            int value = 0;

            if (isBinary || isHexadecimal) {
                try {
                    value = Convert.ToInt32(text.Length > 2 ? text.Substring(2) : throw new FormatException(), @base);
                } catch (Exception e) when (e is OverflowException || e is FormatException) {
                    failed = true;
                }
            } else if (!int.TryParse(text, out value)) {
                failed = true;
            }

            if (failed) {
                var span = new TextSpan(start_, length);
                var location = new TextLocation(text_, span);
                diagnostics.Push(Error.InvalidType(location, text, TypeSymbol.Int));
            } else {
                value_ = value;
            }
        } else {
            if (!double.TryParse(text, out var value)) {
                var span = new TextSpan(start_, length);
                var location = new TextLocation(text_, span);
                diagnostics.Push(Error.InvalidType(location, text, TypeSymbol.Int));
            } else {
                value_ = value;
            }
        }

        type_ = SyntaxType.NUMERIC_LITERAL_TOKEN;
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
                        position_++;
                    break;
            }
        }

        type_ = SyntaxType.WHITESPACE_TRIVIA;
    }

    private void ReadLineBreak() {
        if (current == '\r' && lookahead == '\n')
            position_ += 2;
        else
            position_++;

        type_ = SyntaxType.END_OF_LINE_TRIVIA;
    }

    private void ReadIdentifierOrKeyword() {
        while (char.IsLetterOrDigit(current) || current == '_')
            position_++;

        int length = position_ - start_;
        string text = text_.ToString(start_, length);
        type_ = SyntaxFacts.GetKeywordType(text);
    }
}
