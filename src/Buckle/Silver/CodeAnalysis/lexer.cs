using System.Collections.Generic;

namespace Buckle.CodeAnalysis {

    class Lexer {
        private readonly string text_;
        private int pos_;
        public List<Diagnostic> diagnostics;

        public Lexer(string text) {
            text_ = text;
            diagnostics = new List<Diagnostic>();
        }

        private char current {
            get {
                if (pos_ >= text_.Length) return '\0';
                return text_[pos_];
            }
        }

        private void Advance() { pos_++; }

        public Token Next() {
            if (pos_ >= text_.Length) return new Token(SyntaxType.EOF, pos_, "\0", null);

            if (char.IsDigit(current)) {
                int start = pos_;

                while (char.IsDigit(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);

                if (!int.TryParse(text, out var value))
                    diagnostics.Add(new Diagnostic(DiagnosticType.error, $"'{value}' is not a valid integer"));

                return new Token(SyntaxType.NUMBER, start, text, value);
            } else if (char.IsWhiteSpace(current)) {
                int start = pos_;

                while (char.IsWhiteSpace(current))
                    Advance();

                int length = pos_ - start;
                string text = text_.Substring(start, length);
                return new Token(SyntaxType.WHITESPACE, start, text, null);
            } else if (current == '+') return new Token(SyntaxType.PLUS, pos_++, "+", null);
            else if (current == '-') return new Token(SyntaxType.MINUS, pos_++, "-", null);
            else if (current == '*') return new Token(SyntaxType.ASTERISK, pos_++, "*", null);
            else if (current == '/') return new Token(SyntaxType.SOLIDUS, pos_++, "/", null);
            else if (current == '(') return new Token(SyntaxType.LPAREN, pos_++, "(", null);
            else if (current == ')') return new Token(SyntaxType.RPAREN, pos_++, ")", null);

            diagnostics.Add(new Diagnostic(DiagnosticType.error, $"bad input character '{current}'"));
            return new Token(SyntaxType.Invalid, pos_++, text_.Substring(pos_-1,1), null);
        }
    }

}