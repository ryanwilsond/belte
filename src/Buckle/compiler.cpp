#include "compiler.hpp"

namespace compiler {

Token CreateToken(TokenType type, size_t pos) {
    switch (type) {
        case TokenType::EOFToken: return EndOfFileToken("", pos);
        case TokenType::NUMBER: return NumberToken("", pos, true);
        case TokenType::PLUS: return PlusToken("", pos);
        case TokenType::MINUS: return MinusToken("", pos);
        case TokenType::ASTERISK: return AsteriskToken("", pos);
        case TokenType::SOLIDUS: return SolidusToken("", pos);
        case TokenType::LPAREN: return LParenToken("", pos);
        case TokenType::RPAREN: return RParenToken("", pos);
        case TokenType::WHITESPACE: return WhitespaceToken("", pos);
        default: return InvalidToken("", pos);
    }
}

class Lexer {
private:

    const string text_;
    size_t pos_;

    char Current() {
        if (pos_ >= text_.length()) return '\0';
        return text_[pos_];
    }

public:

    vector<string> Diagnostics;

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

        Diagnostics.push_back(format("bad character input `%c`", Current()));
        pos_++;
        return InvalidToken(text_.substr(pos_-1, 1), pos_);
    }

};

class Parser {
private:

    vector<Token> tokens_;
    size_t pos_;

    Token Peek(int offset) const {
        if (offset < 0) offset *= -1;
        size_t uoff = static_cast<size_t>(offset);
        size_t index = pos_ + uoff;
        if (index >= tokens_.size()) return tokens_[tokens_.size()-1];
        return tokens_[index];
    }

    Token Current() const { return Peek(0); }

    Token Next() {
        auto current = Current();
        pos_++;
        return current;
    }

    Token Match(TokenType type) {
        if (Current().type == type) return Next();
        Diagnostics.push_back(format("unexpected token `%s`, expected token of type `%s`", Current().Type(), Token::Type(type)));
        return CreateToken(type, Current().pos);
    }

public:

    vector<string> Diagnostics;

    Parser(string text) {
        pos_ = 0;
        Lexer lexer = Lexer(text);

        while (true) {
            auto token = lexer.Next();

            if (token.type != TokenType::WHITESPACE && token.type != TokenType::BadToken)
                tokens_.push_back(token);

            if (token.type == TokenType::EOFToken) break;
        }

        Diagnostics.insert(Diagnostics.end(), lexer.Diagnostics.begin(), lexer.Diagnostics.end());
    }

    shared_ptr<Expression> Parse() {
        auto left = ParsePrimary();

        while (Current().type == TokenType::PLUS || Current().type == TokenType::MINUS) {
            auto opTok = Next();
            auto right = ParsePrimary();
            left = make_shared<BinaryExpression>(BinaryExpression(left, make_shared<Token>(opTok), right));
        }

        return left;
    }

    shared_ptr<Expression> ParsePrimary() {
        auto number = Match(TokenType::NUMBER);
        return make_shared<NumberNode>(NumberNode(number));
    }

};

void PrettyPrint(shared_ptr<Node> node, wstring indent, bool last) {
    if (node->type == NodeType::BadNode) return;

    _setmode(_fileno(stdout), _O_U16TEXT);
    wstring marker = last ? L"└─" : L"├─"; // ?: is less readable but easier in this situation
    wcout << indent << marker;
    _setmode(_fileno(stdout), _O_TEXT);
    cout << node->Type() << endl;

    indent += last ? L"  " : L"│ ";

    vector<shared_ptr<Node>> children;

    switch (node->type) {
        case NodeType::NUMBER_EXPR: {
            NumberNode* nodec = dynamic_cast<NumberNode*>(node.get());
            children = nodec->GetChildren();
            break; }
        case NodeType::BINARY_EXPR: {
            BinaryExpression* nodec = dynamic_cast<BinaryExpression*>(node.get());
            children = nodec->GetChildren();
            break; }
        default: {
            children = { };
            break; }
    }

    if (children.size() > 0) {
        auto lastChild = children[children.size()-1];
        for (size_t i=0; i<children.size(); i++) {
            PrettyPrint(children[i], indent, children[i] == lastChild);
        }
    }
}

}

using namespace compiler;

void Compiler::compile() noexcept {
    printf("> ");
    for (string line; getline(cin, line); printf("> ")) {
        if (null_or_whitespace(line)) break;

        Parser parser = Parser(line);
        auto expression = parser.Parse();

        WORD color;
        GetConsoleColor(color);
        SetConsoleColor(COLOR_GRAY);
        PrettyPrint(expression);
        SetConsoleColor(color);

        for (string err : parser.Diagnostics) {
            RaiseError(err);
        }
    }

    exit(0);
}
