#include "toplev.h"

namespace toplev {

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
    bool null;
    NullType(bool n) noexcept { null=n; }
};

#define null NullType(true)

struct SyntaxValue {
    int val_int;
    string val_string;
    NullType val_null = NullType(false);
    SyntaxValue() noexcept { }
};

class SyntaxToken {
public:

    SyntaxType type;
    int pos;
    string text;
    SyntaxValue value;

    void init(SyntaxType _type, int _pos, string _text) noexcept {
        type = _type;
        pos = _pos;
        text = _text;
    }

    SyntaxToken() {}

    SyntaxToken(SyntaxType _type, int _pos, string _text, int _val) noexcept {
        init(_type, _pos, _text);
        value.val_int = _val;
    }

    SyntaxToken(SyntaxType _type, int _pos, string _text, string _val) noexcept {
        init(_type, _pos, _text);
        value.val_string = _val;
    }

    SyntaxToken(SyntaxType _type, int _pos, string _text, NullType _val) noexcept {
        init(_type, _pos, _text);
        value.val_null = _val;
    }

    template<class T>
    _NODISCARD T Value() const {
        switch (type) {
            case SyntaxType::NumberToken:
                if (typeid(T) == typeid(int))
                    return try_cast<T>(value.val_int);
                else throw std::runtime_error(string("Syntax Token is of type int, not ") + typeid(T).name());
            default:
                if (typeid(T) == typeid(NullType))
                    return try_cast<T>(value.val_null);
                else throw std::runtime_error(string("Syntax Token is of type NullType, not ") + typeid(T).name());
        }
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
    size_t pos_;

    char CurrentChar() {
        if (pos_ >= text_.length()) return '\0';
        return text_[pos_];
    }

    void Advance() {
        pos_++;
    }

public:


    Lexer(string text) : text_(text) {
        pos_ = 0;
    }

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

            while (isspace(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);

            return SyntaxToken(SyntaxType::WhitespaceToken, start, text, null);
        } else if (CurrentChar() == '+')
            return SyntaxToken(SyntaxType::PlusToken, pos_++, "+", null);
        else if (CurrentChar() == '-')
            return SyntaxToken(SyntaxType::MinusToken, pos_++, "-", null);
        else if (CurrentChar() == '*')
            return SyntaxToken(SyntaxType::AsteriskToken, pos_++, "*", null);
        else if (CurrentChar() == '/')
            return SyntaxToken(SyntaxType::SolidusToken, pos_++, "/", null);
        else if (CurrentChar() == '(')
            return SyntaxToken(SyntaxType::LeftParenToken, pos_++, "(", null);
        else if (CurrentChar() == ')')
            return SyntaxToken(SyntaxType::RightParenToken, pos_++, ")", null);

        pos_++; // avoids sequence point error
        return SyntaxToken(SyntaxType::BadToken, pos_, text_.substr(pos_-1, 1), null);
    }

};

class SyntaxNode {
public:

    SyntaxType type;

};

class ExpressionSyntax : SyntaxNode {
};

class NumberExpressionSyntax : ExpressionSyntax {
public:

    NumberExpressionSyntax(SyntaxToken number) {
    }

    SyntaxToken number;

};

class BinaryExpressionSyntax : Nu

class Parser {
private:

    vector<SyntaxToken> tokens_;
    int pos_;

    SyntaxToken Peek(int offset) {
        int index = pos_ + offset;

        if (index >= tokens_.size())
            return tokens_[tokens_.size() - 1];

        return tokens_[index];
    }

    SyntaxToken Current() {
        return Peek(0);
    }

public:

    Parser(string text) {
        Lexer lexer = Lexer(text);
        SyntaxToken token;

        while (token.type != SyntaxType::EOFToken) {
            token = lexer.Next();

            if (token.type != SyntaxType::WhitespaceToken &&
                token.type != SyntaxType::BadToken) {
                tokens_.push_back(token);
            }
        }
    }
};


int main() noexcept {
    int error = SUCCESS_EXIT;

    printf("> ");
    for (string line; std::getline(std::cin, line); printf("> ")) {
        if (null_or_whitespace(line)) return error;

        auto lexer = Lexer(line);
        while (true) {
            auto token = lexer.Next();
            if (token.type == SyntaxType::EOFToken) break;
            cout << token.Type() << ": '" << token.text << "' ";

            if (token.type == SyntaxType::NumberToken)
                cout << token.Value<int>();

            cout << endl;
        }
    }

    return SUCCESS_EXIT;
}

}
