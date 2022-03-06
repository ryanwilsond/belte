#include "compiler.hpp"

class Lexer {
private:

    const string text_;
    size_t pos_;

    char Current() {
        if (pos_ >= text_.length()) return '\0';
        return text_[pos_];
    }

public:

    Lexer(string _text) : text_(_text) {
        pos_ = 0;
    }

    Token Next() {
        // numbers, * - / *, whitespace
        if (pos_ >= text_.length()) return EndOfFileToken("\0", pos_);

        if (isdigit(Current())) {
            auto start = pos_;

            while (isdigit(Current())) pos_++;

            auto len = pos_ - start;
            string text = text_.substr(start, len);
            int value = stoi(text);

            return NumberToken(text, start, value);
        } else if (isspace(Current())) {
            auto start = pos_;

            while (isspace(Current())) pos_++;

            auto len = pos_ - start;
            string text = text_.substr(start, len);

            return WhitespaceToken(text, start);
        } else if (Current() == '+') return PlusToken("+", pos_++);
        else if (Current() == '-') return MinusToken("-", pos_++);
        else if (Current() == '*') return AsteriskToken("*", pos_++);
        else if (Current() == '/') return SolidusToken("/", pos_++);
        else if (Current() == '(') return LParenToken("(", pos_++);
        else if (Current() == ')') return RParenToken(")", pos_++);

        pos_++;
        return InvalidToken(text_.substr(pos_-1, 1), pos_);
    }

};

void Compiler::compile() noexcept {
    printf("> ");
    for (string line; getline(cin, line); printf("> ")) {
        Lexer lexer = Lexer(line);
        if (null_or_whitespace(line)) break;

        while (true) {
            auto tok = lexer.Next();
            if (tok.type == SyntaxTokenType::EOFToken) break;
            if (tok.type == SyntaxTokenType::WHITESPACE) continue;

            printf("%s: '%s' ", tok.Type().c_str(), tok.text.c_str());
            if (tok.type == SyntaxTokenType::NUMBER) cout << tok.val_int;
            printf("\n");
        }
    }

    exit(0);
}
