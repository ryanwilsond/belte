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
};

class ParamBase {
public:

    virtual ~ParamBase() {}

    template<class T> const T& get() const;
    template<class T, class U> void setValue(const U& val);

};

template <typename T>
class Param : public ParamBase {
private:
    T value;

public:

    Param(const T& val) : value(val) {}

    const T& get() const {
        return value;
    }

    void setValue(const T& val) {
        value = val;
    }

};

template<class T> const T& ParamBase::get() const {
    return dynamic_cast<const Param<T>&>(*this).get();
}

template<class T, class U> void ParamBase::setValue(const U& val) {
    return dynamic_cast<Param<T>&>(*this).setValue(val);
}

class SyntaxToken {
public:

    SyntaxType type;
    int pos;
    string text;
    ParamBase value;

    SyntaxToken(SyntaxType _type, int _pos, string _text, ParamBase _value) {
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

#define null_t Param<NullType>(NullType())

template <class T> Param<T> param(T val) {
    return Param<T>(val);
}

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
            return SyntaxToken(SyntaxType::EOFToken, pos_, "\0", null_t);

        if (isdigit(CurrentChar())) {
            auto start = pos_;

            while (isdigit(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);
            int value = stoi(text);

            return SyntaxToken(SyntaxType::NumberToken, start, text, param(value));
        } else if (isspace(CurrentChar())) {
            auto start = pos_;

            while (isspace(CurrentChar()))
                Advance();

            auto len = pos_ - start;
            auto text = text_.substr(start, len);

            return SyntaxToken(SyntaxType::WhitespaceToken, start, text, null_t);
        } else if (CurrentChar() == '+')
            return SyntaxToken(SyntaxType::PlusToken, pos_++, "+", null_t);
        else if (CurrentChar() == '-')
            return SyntaxToken(SyntaxType::MinusToken, pos_++, "-", null_t);
        else if (CurrentChar() == '*')
            return SyntaxToken(SyntaxType::AsteriskToken, pos_++, "*", null_t);
        else if (CurrentChar() == '/')
            return SyntaxToken(SyntaxType::SolidusToken, pos_++, "/", null_t);
        else if (CurrentChar() == '(')
            return SyntaxToken(SyntaxType::LeftParenToken, pos_++, "(", null_t);
        else if (CurrentChar() == ')')
            return SyntaxToken(SyntaxType::RightParenToken, pos_++, ")", null_t);

        return SyntaxToken(SyntaxType::BadToken, pos_++, text_.substr(pos_-1, 1), null_t);
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
                cout << token.text;
            cout << endl;
        }
    }

    return SUCCESS_EXIT;
}

}
