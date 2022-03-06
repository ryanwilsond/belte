#include "toplev.h"

namespace toplev {

enum SyntaxType {
    NumberToken,
    WhitespaceToken,
    PlusToken,
    MinusToken,
    AsteriskToken,
    SolidusToken,
    OpenParenToken,
    CloseParenToken,
    BadToken,
    EOFToken,
    NumberExpression,
    BinaryExpression,
};

struct NullType {
    bool is_null;
    NullType(bool n) noexcept { is_null=n; }
};

#define null NullType(true)

struct SyntaxValue {
    int val_int;
    string val_string;
    NullType val_null = NullType(false);
    SyntaxValue() noexcept { }
};

class SyntaxNode {
public:

    SyntaxType type;

    vector<SyntaxNode> GetChildren();

    string Type() const {
        switch (type) {
            case SyntaxType::NumberToken: return "NumberToken";
            case SyntaxType::WhitespaceToken: return "WhitespaceToken";
            case SyntaxType::PlusToken: return "PlusToken";
            case SyntaxType::MinusToken: return "MinusToken";
            case SyntaxType::AsteriskToken: return "AsteriskToken";
            case SyntaxType::SolidusToken: return "SolidusToken";
            case SyntaxType::OpenParenToken: return "OpenParenToken";
            case SyntaxType::CloseParenToken: return "CloseParenToken";
            case SyntaxType::BadToken: return "BadToken";
            case SyntaxType::EOFToken: return "EOFToken";
            default: return "NullToken";
        }
    }

};

class SyntaxToken : public SyntaxNode {
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

    vector<SyntaxNode> GetChildren() {
        return vector<SyntaxNode>();
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
            return SyntaxToken(SyntaxType::OpenParenToken, pos_++, "(", null);
        else if (CurrentChar() == ')')
            return SyntaxToken(SyntaxType::CloseParenToken, pos_++, ")", null);

        pos_++; // avoids sequence point error
        return SyntaxToken(SyntaxType::BadToken, pos_, text_.substr(pos_-1, 1), null);
    }

};

class ExpressionSyntax : public SyntaxNode {
};

class NumberExpressionSyntax : public ExpressionSyntax {
public:

    const SyntaxType type = SyntaxType::NumberExpression;
    SyntaxToken number;

    NumberExpressionSyntax(SyntaxToken _number) {
        number = _number;
    }

    vector<SyntaxNode> GetChildren() {
        vector<SyntaxNode> nodes = {number};
        return nodes;
    }

};

class BinaryExpressionSyntax : public ExpressionSyntax {
public:

    ExpressionSyntax left;
    SyntaxToken op;
    ExpressionSyntax right;
    const SyntaxType type = SyntaxType::BinaryExpression;

    BinaryExpressionSyntax(ExpressionSyntax _left, SyntaxToken _op, ExpressionSyntax _right) {
        left = _left;
        op = _op;
        right = _right;
    }

    vector<SyntaxNode> GetChildren() {
        vector<SyntaxNode> nodes = {left, op, right};
        return nodes;
    }

};

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

    SyntaxToken NextToken() {
        SyntaxToken current = Current();
        pos_++;
        return current;
    }

    SyntaxToken Match(SyntaxType _type) {
        if (Current().type == _type) return NextToken();
        return SyntaxToken(_type, Current().pos, "", null);
    }

    ExpressionSyntax ParsePrimaryExpression() {
        SyntaxToken number = Match(SyntaxType::NumberToken);
        return NumberExpressionSyntax(number);
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

    ExpressionSyntax Parse() {
        auto left = ParsePrimaryExpression();

        while (Current().type == SyntaxType::PlusToken ||
               Current().type == SyntaxType::MinusToken) {
            SyntaxToken opToken = NextToken();
            auto right = ParsePrimaryExpression();
            left = BinaryExpressionSyntax(left, opToken, right);
        }

        return left;
    }

};

void PrettyPrint(SyntaxNode node, string index = "") {
    cout << node.Type();

    SyntaxToken *nodeChild = reinterpret_cast<SyntaxToken*>(&node);

    if (nodeChild->value.val_null.is_null == false) {
        
    }

}

int main() {
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
