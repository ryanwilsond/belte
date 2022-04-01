#include "compiler.hpp"

namespace compiler {

Token CreateToken(TokenType type, size_t pos) {
    switch (type) {
        case TokenType::EOFToken: return EndOfFileToken("", pos);
        case TokenType::NUMBER: return NumberToken("", pos, true);
        case TokenType::PLUS: return PlusToken("", pos);
        case TokenType::MINUS: return MinusToken("", pos);
        case TokenType::ASTERISK: return AsteriskToken("", pos);
        case TokenType::SLASH: return SolidusToken("", pos);
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
            int value;

            try {
                value = stoi(text);
            } catch (...) {
                Diagnostics.push_back(format("'%s' cannot be represented as an integer", text.c_str()));
            }

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
        Diagnostics.push_back(format("unexpected token `%s`, expected token of type `%s`", Current().Type().c_str(), Token::Type(type).c_str()));
        return CreateToken(type, Current().pos);
    }

    shared_ptr<Expression> ParseExpression() {
        return ParseTerm();
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

    SyntaxTree Parse() {
        auto expr = ParseTerm();
        auto eofToken = Match(TokenType::EOFToken);
        return SyntaxTree(Diagnostics, expr, eofToken);
    }

    shared_ptr<Expression> ParseTerm() {
        auto left = ParseFactor();

        while (Current().type == TokenType::PLUS || Current().type == TokenType::MINUS) {
            auto opTok = Next();
            auto right = ParseFactor();
            left = make_shared<BinaryExpression>(BinaryExpression(left, make_shared<Token>(opTok), right));
        }

        return left;
    }

    shared_ptr<Expression> ParseFactor() {
        auto left = ParsePrimary();

        while (Current().type == TokenType::ASTERISK || Current().type == TokenType::SLASH) {
            auto opTok = Next();
            auto right = ParsePrimary();
            left = make_shared<BinaryExpression>(BinaryExpression(left, make_shared<Token>(opTok), right));
        }

        return left;
    }

    shared_ptr<Expression> ParsePrimary() {
        if (Current().type == TokenType::LPAREN) {
            auto left = Next();
            auto expr = ParseExpression();
            auto right = Match(TokenType::RPAREN);
            return make_shared<Expression>(ParenExpression(make_shared<Token>(left), expr, make_shared<Token>(right)));
        }

        auto number = Match(TokenType::NUMBER);
        return make_shared<NumberNode>(NumberNode(number));
    }

};

class Evaluator {
private:

    shared_ptr<Expression> root_;

    int EvaluateExpression(shared_ptr<Expression> node) {
        if (node.get()->type == NodeType::NUMBER_EXPR) {
            return dynamic_cast<NumberNode*>(node.get())->token.val_int;
        } else if (node.get()->type == NodeType::BINARY_EXPR) {
            printf("after\n");
            BinaryExpression *bi_expr = dynamic_cast<BinaryExpression*>(node.get());
            auto left = EvaluateExpression(bi_expr->left);
            auto right = EvaluateExpression(bi_expr->right);

            switch (bi_expr->op.get()->type) {
                case TokenType::PLUS:
                    return left + right;
                case TokenType::MINUS:
                    return left - right;
                case TokenType::ASTERISK:
                    return left * right;
                case TokenType::SLASH:
                    return left / right;
                default:
                    throw std::runtime_error(format("Unexpected binary operator `%s`", bi_expr->op.get()->Type().c_str()));
            }
        } else if (node.get()->type == NodeType::PAREN_EXPR) {
            ParenExpression *par_expr = dynamic_cast<ParenExpression*>(node.get());
            printf("seg?\n");
            return EvaluateExpression(par_expr->Expr);
        }

        throw std::runtime_error(format("Unexpected node `%s`", node.get()->Type().c_str()));
    }

public:

    Evaluator(shared_ptr<Expression> root) {
        root_ = root;
    }

    int Evaluate() {
        return EvaluateExpression(root_);
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
        auto syntaxTree = parser.Parse();

        WORD color;
        GetConsoleColor(color);
        SetConsoleColor(COLOR_GRAY);
        PrettyPrint(syntaxTree.Root);
        SetConsoleColor(color);

        if (syntaxTree.Diagnostics.size() > 0) {
            for (string err : syntaxTree.Diagnostics) {
                RaiseError(err);
            }
        } else {
            auto eval = Evaluator(syntaxTree.Root);
            auto result = eval.Evaluate();
            cout << result << endl;
        }
    }

    exit(0);
}
