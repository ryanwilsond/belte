#include "toplev.h"

namespace toplev {

int main() noexcept {
    int error = SUCCESS_EXIT;

    printf("> ");
    for (string line; std::getline(std::cin, line); printf("> ")) {
        if (null_or_whitespace(line)) return error;

        if (line == "1 + 2 * 3") printf("%i\n", 7);
        else printf("ERROR: Invalid Expression\n");
    }

    return SUCCESS_EXIT;
}


enum SyntaxType {
    Number,
};

class SyntaxToken {
public:

    SyntaxType type_;
    int pos_;
    string text_;

    SyntaxToken(SyntaxType type, int pos, string text) {
        type_ = type;
        pos_ = pos;
        text_ = text;
    }

};

class Lexer {
private:
    const string text_;
    int pos_;

    char CurrentChar() {
        if (pos_ >= text_.size()) return '\0';
        return text_[pos_++];
    }

    void Advance() {
        pos_++;
    }

public:

    Lexer(string text) : text_(text) {}

    SyntaxToken Next() {
        // <numbers>
        // + - * / ( )
        // <whitespace>

        if (isdigit(CurrentChar())) {
            auto start = pos_;

            while (isdigit(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);
            int value = 

            return SyntaxToken(SyntaxType::Number, start, text, value);
        }
    }

};


}
