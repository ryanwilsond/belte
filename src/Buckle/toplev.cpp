#include "toplev.h"

namespace toplev {

int main() noexcept {
    int error = SUCCESS_EXIT;

    printf("> ");
    for (string line; std::getline(std::cin, line); printf("> ")) {
        if (null_or_whitespace(line)) return error;

        auto lexer = Lexer(line);
        while (true) {
            SyntaxToken<class T> token = lexer.Next<T>();
            if (token.type == SyntaxType::EOFToken) break;
            cout << token.Type() << ": " << token.text << endl;
            if (token.value != NULL)
                cout << token.value << endl;
        }

        if (line == "1 + 2 * 3") printf("%i\n", 7);
        else printf("ERROR: Invalid Expression\n");
    }

    return SUCCESS_EXIT;
}


enum SyntaxType {
    NumberToken,
    WhitespaceToken,
    PlusToken,
    MinusToken,
    AsteriskToken,
    SolidusToken,
    LeftParenToken,
    RightParenToken,
    BadToken,
    EOFToken,
};

struct NullType {
};

class SyntaxToken {
public:

    SyntaxType type;
    int pos;
    string text;
    boost::any value;

    SyntaxToken(SyntaxType _type, int _pos, string _text, T _value) {
        type = _type;
        pos = _pos;
        text = _text;
        value = _value;
    }

    string Type() const {
        switch (type) {
            case SyntaxType::NumberToken: return "NumberToken";
            case SyntaxType::WhitespaceToken: return "WhitespaceToken";
            case SyntaxType::PlusToken: return "PlusToken";
            case SyntaxType::MinusToken: return "MinusToken";
            case SyntaxType::AsteriskToken: return "AsteriskToken";
            case SyntaxType::SolidusToken: return "SolidusToken";
            case SyntaxType::LeftParenToken: return "LeftParenToken";
            case SyntaxType::RightParenToken: return "RightParenToken";
            case SyntaxType::BadToken: return "BadToken";
            case SyntaxType::EOFToken: return "EOFToken";
            default: return "NullToken";
        }
    }

};

class Lexer {
private:
    const string text_;
    int pos_;

    char CurrentChar() {
        if (pos_ >= text_.length()) return '\0';
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

        if (pos_ >= text_.length())
            return SyntaxToken(SyntaxType::EOFToken, pos_, "\0", null);

        if (isdigit(CurrentChar())) {
            auto start = pos_;

            while (isdigit(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);
            int value = stoi(text);

            return SyntaxToken(SyntaxType::NumberToken, start, text, value);
        } else if (isspace(CurrentChar())) {
            auto start = pos_;

            while (isdigit(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);
            int value = stoi(text);

            return SyntaxToken(SyntaxType::WhitespaceToken, start, text, value);
        } else if (CurrentChar() == '+')
            return SyntaxToken(SyntaxType::PlusToken, pos_++, "+", NULL);
        else if (CurrentChar() == '-')
            return SyntaxToken(SyntaxType::MinusToken, pos_++, "-", NULL);
        else if (CurrentChar() == '*')
            return SyntaxToken(SyntaxType::AsteriskToken, pos_++, "*", NULL);
        else if (CurrentChar() == '/')
            return SyntaxToken(SyntaxType::SolidusToken, pos_++, "/", NULL);
        else if (CurrentChar() == '(')
            return SyntaxToken(SyntaxType::LeftParenToken, pos_++, "(", NULL);
        else if (CurrentChar() == ')')
            return SyntaxToken(SyntaxType::RightParenToken, pos_++, ")", NULL);

        return SyntaxToken(SyntaxType::BadToken, pos_++, text_.substr(pos_-1, 1), NULL);
    }

};


}
