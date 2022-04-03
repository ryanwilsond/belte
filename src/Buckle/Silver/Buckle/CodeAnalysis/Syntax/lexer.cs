using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Lexer {
        private readonly SourceText text_;
        private int position_;
        private int start_;
        private SyntaxType type_;
        private object value_;
        public DiagnosticQueue diagnostics;

        public Lexer(SourceText text) {
            text_ = text;
            diagnostics = new DiagnosticQueue();
        }

        private char Peek(int offset) {
            int index = position_ + offset;
            if (index >= text_.length) return '\0';
            return text_[index];
        }

        private char current => Peek(0);
        private char lookahead => Peek(1);

        public Token LexNext() {
            start_ = position_;
            type_ = SyntaxType.Invalid;
            value_ = null;

            switch (current) {
                case '\0':
                    type_ = SyntaxType.EOF;
                    break;
                case '/':
                    position_++;
                    type_ = SyntaxType.SLASH;
                    break;
                case '(':
                    position_++;
                    type_ = SyntaxType.LPAREN;
                    break;
                case ')':
                    position_++;
                    type_ = SyntaxType.RPAREN;
                    break;
                case '{':
                    position_++;
                    type_ = SyntaxType.LBRACE;
                    break;
                case '}':
                    position_++;
                    type_ = SyntaxType.RBRACE;
                    break;
                case ';':
                    position_++;
                    type_ = SyntaxType.SEMICOLON;
                    break;
                case '~':
                    position_++;
                    type_ = SyntaxType.TILDE;
                    break;
                case '^':
                    position_++;
                    type_ = SyntaxType.CARET;
                    break;
                case '+':
                    position_++;
                    // if (current == '+') {
                    //     type_ = SyntaxType.DPLUS;
                    //     position_++;
                    // } else {
                    type_ = SyntaxType.PLUS;
                    // }
                    break;
                case '-':
                    position_++;
                    // if (current == '-') {
                    //     type_ = SyntaxType.DMINUS;
                    //     position_++;
                    // } else {
                    type_ = SyntaxType.MINUS;
                    // }
                    break;
                case '*':
                    position_++;
                    if (current == '*') {
                        type_ = SyntaxType.DASTERISK;
                        position_++;
                    } else {
                        type_ = SyntaxType.ASTERISK;
                    }
                    break;
                case '&':
                    position_++;
                    if (current == '&') {
                        type_ = SyntaxType.DAMPERSAND;
                        position_++;
                    } else {
                        type_ = SyntaxType.AMPERSAND;
                    }
                    break;
                case '|':
                    position_++;
                    if (current == '|') {
                        type_ = SyntaxType.DPIPE;
                        position_++;
                    } else {
                        type_ = SyntaxType.PIPE;
                    }
                    break;
                case '=':
                    position_++;
                    if (current == '=') {
                        type_ = SyntaxType.DEQUALS;
                        position_++;
                    } else {
                        type_ = SyntaxType.EQUALS;
                    }
                    break;
                case '!':
                    position_++;
                    if (current == '=') {
                        position_++;
                        type_ = SyntaxType.BANGEQUALS;
                    } else {
                        type_ = SyntaxType.BANG;
                    }
                    break;
                case '<':
                    position_++;
                    if (current == '=') {
                        position_++;
                        type_ = SyntaxType.LESSEQUAL;
                    } else if (current == '<') {
                        position_++;
                        type_ = SyntaxType.SHIFTLEFT;
                    } else {
                        type_ = SyntaxType.LANGLEBRACKET;
                    }
                    break;
                case '>':
                    position_++;
                    if (current == '=') {
                        position_++;
                        type_ = SyntaxType.GREATEQUAL;
                    } else if (current == '>') {
                        position_++;
                        type_ = SyntaxType.SHIFTRIGHT;
                    } else {
                        type_ = SyntaxType.RANGLEBRACKET;
                    }
                    break;
                case '"':
                    ReadString();
                    break;
                case '0': // faster than if check, but probably neglagable and is uglier
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    ReadNumberToken();
                    break;
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    ReadWhitespaceToken();
                    break;
                default:
                    if (char.IsLetter(current)) ReadIdentifierOrKeyword();
                    else if (char.IsWhiteSpace(current)) ReadWhitespaceToken();
                    else diagnostics.Push(Error.BadCharacter(position_++, current));
                    break;
            }

            int length = position_ - start_;
            var text = SyntaxFacts.GetText(type_);
            if (text == null)
                text = text_.ToString(start_, length);

            return new Token(type_, start_, text, value_);
        }

        private void ReadString() {
            position_++;
            var sb = new StringBuilder();
            bool done = false;

            while (!done) {
                switch (current) {
                    case '\0':
                    case '\r':
                    case '\n':
                        var span = new TextSpan(start_, 1);
                        diagnostics.Push(Error.UnterminatedString(span));
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
                    default:
                        sb.Append(current);
                        position_++;
                        break;
                }
            }

            type_ = SyntaxType.STRING;
            value_ = sb.ToString();
        }

        private void ReadNumberToken() {
            while (char.IsDigit(current)) position_++;

            int length = position_ - start_;
            string text = text_.ToString(start_, length);

            if (!int.TryParse(text, out var value))
                diagnostics.Push(Error.InvalidType(new TextSpan(start_, length), text, TypeSymbol.Int));

            value_ = value;
            type_ = SyntaxType.NUMBER;
        }

        private void ReadWhitespaceToken() {
            while (char.IsWhiteSpace(current)) position_++;
            type_ = SyntaxType.WHITESPACE;
        }

        private void ReadIdentifierOrKeyword() {
            while (char.IsLetter(current)) position_++;

            int length = position_ - start_;
            string text = text_.ToString(start_, length);
            type_ = SyntaxFacts.GetKeywordType(text);
        }
    }
}
