using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {

    internal class Lexer {
        private readonly string text_;
        private int pos_;
        public DiagnosticQueue diagnostics;

        public Lexer(string text) {
            text_ = text;
            diagnostics = new DiagnosticQueue();
        }

        private char Peek(int offset) {
            int index = pos_ + offset;
            if (index >= text_.Length) return '\0';
            return text_[index];
        }

        private char current => Peek(0);
        private char lookahead => Peek(1);

        private void Advance() { pos_++; }

        public Token LexNext() {
            if (pos_ >= text_.Length) return new Token(SyntaxType.EOF, pos_, "\0", null);

            int start = pos_;

            if (char.IsDigit(current)) {
                while (char.IsDigit(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);

                if (!int.TryParse(text, out var value))
                    diagnostics.Push(Error.InvalidType(new TextSpan(start, length), text, typeof(int)));

                return new Token(SyntaxType.NUMBER, start, text, value);
            } else if (char.IsWhiteSpace(current)) {
                while (char.IsWhiteSpace(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                return new Token(SyntaxType.WHITESPACE, start, text, null);
            } else if (char.IsLetter(current)) {
                while (char.IsLetter(current)) Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                var type = SyntaxFacts.GetKeywordType(text);
                return new Token(type, start, text, null);
            }

            switch (current) {
                case '+': return new Token(SyntaxType.PLUS, pos_++, "+", null);
                case '-': return new Token(SyntaxType.MINUS, pos_++, "-", null);
                case '*': return new Token(SyntaxType.ASTERISK, pos_++, "*", null);
                case '/': return new Token(SyntaxType.SOLIDUS, pos_++, "/", null);
                case '(': return new Token(SyntaxType.LPAREN, pos_++, "(", null);
                case ')': return new Token(SyntaxType.RPAREN, pos_++, ")", null);
                case '&':
                    if (lookahead == '&') {
                        pos_+=2;
                        return new Token(SyntaxType.DAMPERSAND, start, "&&", null);
                    }
                    break;
                case '|':
                    if (lookahead == '|') {
                        pos_+=2;
                        return new Token(SyntaxType.DPIPE, start, "||", null);
                    }
                    break;
                case '=':
                    if (lookahead == '=') {
                        pos_+=2;
                        return new Token(SyntaxType.DEQUALS, start, "==", null);
                    }
                    return new Token(SyntaxType.EQUALS, pos_++, "=", null);
                case '!':
                    if (lookahead == '=') {
                        pos_+=2;
                        return new Token(SyntaxType.BANGEQUALS, start, "!=", null);
                    }
                    return new Token(SyntaxType.BANG, pos_++, "!", null);
                default: break;
            }

            diagnostics.Push(Error.BadCharacter(pos_, current));
            return new Token(SyntaxType.Invalid, pos_++, text_.Substring(pos_-1,1), null);
        }
    }
}
