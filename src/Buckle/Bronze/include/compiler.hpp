/* Compiler entry point */
#pragma once
#ifndef COMPILER_HPP
#define COMPILER_HPP

#include "utils.hpp"

namespace compiler {

enum TokenType {
    EOFToken,
    BadToken,
    NUMBER,
    PLUS,
    MINUS,
    ASTERISK,
    SLASH,
    LPAREN,
    RPAREN,
    WHITESPACE,
};

enum NodeType {
    BadNode,
    TokenNode,
    NUMBER_EXPR,
    BINARY_EXPR,
    UNARY_EXPR,
    PAREN_EXPR,
};

class Node {
public:
    const NodeType type;
    virtual vector<shared_ptr<Node>> GetChildren() const { return { }; }

    Node() : type(NodeType::BadNode) { }
    Node(NodeType _type) : type(_type) { }

    string Type() const {
        switch (type) {
            case NodeType::BadNode: return "InvalidNode";
            case NodeType::TokenNode: return "TokenNode";
            case NodeType::NUMBER_EXPR: return "NumberNode";
            case NodeType::BINARY_EXPR: return "BinaryExpression";
            case NodeType::UNARY_EXPR: return "UnaryExpression";
            default: return "UnknownNode";
        }
    }

};

class Token : public Node {
public:

    string text;
    TokenType type;
    size_t pos;
    int val_int;
    bool is_null = true;

    Token() : Node(NodeType::TokenNode), type(TokenType::BadToken) { }
    Token(TokenType _type) : Node(NodeType::TokenNode), type(_type) { }

    static string Type(TokenType _type) {
        switch (_type) {
            case TokenType::EOFToken: return "EOFToken";
            case TokenType::BadToken: return "InvalidToken";
            case TokenType::NUMBER: return "NumberToken";
            case TokenType::PLUS: return "PLUS";
            case TokenType::MINUS: return "MINUS";
            case TokenType::ASTERISK: return "ASTERISK";
            case TokenType::SLASH: return "SLASH";
            case TokenType::LPAREN: return "LPAREN";
            case TokenType::RPAREN: return "RPAREN";
            case TokenType::WHITESPACE: return "WHITESPACE";
            default: return "UnknownToken";
        }
    }

    string Type() const { return Token::Type(type); }

    void operator=(const Token& token) {
        text = token.text;
        type = token.type;
        pos = token.pos;
        val_int = token.val_int;
        is_null = token.is_null;
    }

};

class EndOfFileToken : public Token {
public:
    EndOfFileToken(string _text, size_t _pos) : Token(TokenType::EOFToken) {
        text = _text;
        pos = _pos;
    }
};

class NumberToken : public Token {
public:
    NumberToken(string _text, size_t _pos, int _value) : Token(TokenType::NUMBER) {
        text = _text;
        pos = _pos;
        val_int = _value;
        is_null = false;
    }

    NumberToken(string _text, size_t _pos, bool _value): Token(TokenType::NUMBER) {
        text = _text;
        pos = _pos;
        is_null = _value;
    }
};

class WhitespaceToken : public Token {
public:
    WhitespaceToken(string _text, size_t _pos) : Token(TokenType::WHITESPACE) {
        text = _text;
        pos = _pos;
    }
};

class PlusToken : public Token {
public:
    PlusToken(string _text, size_t _pos) : Token(TokenType::PLUS) {
        text = _text;
        pos = _pos;
    }
};

class MinusToken : public Token {
public:
    MinusToken(string _text, size_t _pos) : Token(TokenType::MINUS) {
        text = _text;
        pos = _pos;
    }
};

class AsteriskToken : public Token {
public:
    AsteriskToken(string _text, size_t _pos) : Token(TokenType::ASTERISK) {
        text = _text;
        pos = _pos;
    }
};

class SolidusToken : public Token {
public:
    SolidusToken(string _text, size_t _pos) : Token(TokenType::SLASH) {
        text = _text;
        pos = _pos;
    }
};

class LParenToken : public Token {
public:
    LParenToken(string _text, size_t _pos) : Token(TokenType::LPAREN) {
        text = _text;
        pos = _pos;
    }
};

class RParenToken : public Token {
public:
    RParenToken(string _text, size_t _pos) : Token(TokenType::RPAREN) {
        text = _text;
        pos = _pos;
    }
};

class InvalidToken : public Token {
public:
    InvalidToken(string _text, size_t _pos) : Token(TokenType::BadToken) {
        text = _text;
        pos = _pos;
    }
};

class Expression : public Node {
public:
    Expression(NodeType _type) : Node(_type) { }
};

class InvalidNode : public Node {
public:
    InvalidNode() : Node(NodeType::BadNode) { }
};

class NumberNode : public Expression {
public:
    Token token;

    NumberNode(Token _token) : Expression(NodeType::NUMBER_EXPR), token(_token) { }
    vector<shared_ptr<Node>> GetChildren() const { return { make_shared<Token>(token) }; }
};

class BinaryExpression : public Expression {
public:
    shared_ptr<Expression> left;
    shared_ptr<Token> op;
    shared_ptr<Expression> right;

    BinaryExpression(shared_ptr<Expression> _left, shared_ptr<Token> _op, shared_ptr<Expression> _right) : Expression(NodeType::BINARY_EXPR) {
        left = _left;
        op = _op;
        right = _right;
    }
    vector<shared_ptr<Node>> GetChildren() const { return { left, op, right }; }
};

class UnaryExpression : public Expression {

};

class ParenExpression : public Expression {
public:

    shared_ptr<Token> OpenParen;
    shared_ptr<Expression> Expr;
    shared_ptr<Token> CloseParen;

    ParenExpression(shared_ptr<Token> openParen, shared_ptr<Expression> expr, shared_ptr<Token> closeParen) : Expression(NodeType::PAREN_EXPR) {
        OpenParen = openParen;
        Expr = expr;
        CloseParen = closeParen;
    }

    vector<shared_ptr<Node>> GetChildren() const { return { OpenParen, Expr, CloseParen }; }
};

class SyntaxTree {
public:

    vector<string> Diagnostics;
    shared_ptr<Expression> Root;
    Token EOFToken;

    SyntaxTree(vector<string> diagnostics, shared_ptr<Expression> root, Token EOFtoken) {
        Root = root;
        EOFToken = EOFtoken;
        Diagnostics = diagnostics;
    }

};

void PrettyPrint(shared_ptr<Node> node, wstring index=L"", bool last=true);
Token CreateToken(TokenType type, size_t pos);

}

class Compiler {
private:

    Compiler() {}

public:

    static void compile() noexcept;

};

#endif
