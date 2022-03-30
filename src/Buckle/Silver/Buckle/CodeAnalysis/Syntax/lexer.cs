using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Lexer {
        private readonly SourceText text_;
        private int pos_;
        private int start_;
        private SyntaxType type_;
        private object value_;
        public DiagnosticQueue diagnostics;

        public Lexer(SourceText text) {
            text_ = text;
            diagnostics = new DiagnosticQueue();
        }

        private char Peek(int offset) {
            int index = pos_ + offset;
            if (index >= text_.length) return '\0';
            return text_[index];
        }

        private char current => Peek(0);
        private char lookahead => Peek(1);

        public Token LexNext() {
            start_ = pos_;
            type_ = SyntaxType.Invalid;
            value_ = null;

            switch (current) {
                case '\0':
                    type_= SyntaxType.EOF;
                    break;
                case '/':
                    pos_++;
                    type_= SyntaxType.SOLIDUS;
                    break;
                case '(':
                    pos_++;
                    type_= SyntaxType.LPAREN;
                    break;
                case ')':
                    pos_++;
                    type_= SyntaxType.RPAREN;
                    break;
                case '{':
                    pos_++;
                    type_ = SyntaxType.LBRACE;
                    break;
                case '}':
                    pos_++;
                    type_ = SyntaxType.RBRACE;
                    break;
                case ';':
                    pos_++;
                    type_ = SyntaxType.SEMICOLON;
                    break;
                case '+':
                    pos_++;
                    // if (current == '+') {
                    //     type_ = SyntaxType.DPLUS;
                    //     pos_++;
                    // } else {
                        type_= SyntaxType.PLUS;
                    // }
                    break;
                case '-':
                    pos_++;
                    // if (current == '-') {
                    //     type_ = SyntaxType.DMINUS;
                    //     pos_++;
                    // } else {
                        type_= SyntaxType.MINUS;
                    // }
                    break;
                case '*':
                    pos_++;
                    if (current == '*') {
                        type_ = SyntaxType.DASTERISK;
                        pos_++;
                    } else {
                        type_= SyntaxType.ASTERISK;
                    }
                    break;
                case '&':
                    if (lookahead == '&') {
                        type_= SyntaxType.DAMPERSAND;
                        pos_+=2;
                    } else goto default;
                    break;
                case '|':
                    if (lookahead == '|') {
                        type_= SyntaxType.DPIPE;
                        pos_+=2;
                    } else goto default;
                    break;
                case '=':
                    pos_++;
                    if (current == '=') {
                        type_= SyntaxType.DEQUALS;
                        pos_++;
                    } else {
                        type_= SyntaxType.EQUALS;
                    }
                    break;
                case '!':
                    pos_++;
                    if (current == '=') {
                        pos_++;
                        type_= SyntaxType.BANGEQUALS;
                    } else {
                        type_= SyntaxType.BANG;
                    }
                    break;
                case '<':
                    pos_++;
                    if (current == '=') {
                        pos_++;
                        type_= SyntaxType.LESSEQUAL;
                    } else {
                        type_= SyntaxType.LANGLEBRACKET;
                    }
                    break;
                case '>':
                    pos_++;
                    if (current == '=') {
                        pos_++;
                        type_= SyntaxType.GREATEQUAL;
                    } else {
                        type_= SyntaxType.RANGLEBRACKET;
                    }
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
                    else diagnostics.Push(Error.BadCharacter(pos_++, current));
                    break;
            }

            int length = pos_ - start_;
            var text = SyntaxFacts.GetText(type_);
            if (text == null)
                text = text_.ToString(start_, length);

            return new Token(type_, start_, text, value_);
        }

        private void ReadNumberToken() {
            while (char.IsDigit(current)) pos_++;

            int length = pos_ - start_;
            string text = text_.ToString(start_, length);

            if (!int.TryParse(text, out var value))
                diagnostics.Push(Error.InvalidType(new TextSpan(start_, length), text, typeof(int)));

            value_ = value;
            type_ = SyntaxType.NUMBER;
        }

        private void ReadWhitespaceToken() {
            while (char.IsWhiteSpace(current)) pos_++;
            type_ = SyntaxType.WHITESPACE;
        }

        private void ReadIdentifierOrKeyword() {
            while (char.IsLetter(current)) pos_++;

            int length = pos_ - start_;
            string text = text_.ToString(start_, length);
            type_ = SyntaxFacts.GetKeywordType(text);
        }
    }
}
