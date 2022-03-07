/* Compiler entry point */
#pragma once
#ifndef COMPILER_HPP
#define COMPILER_HPP

#include "utils.hpp"

using std::cout;
using std::getline;
using std::cin;
using std::endl;

namespace compiler {

enum TokenType {
    EOFToken,
    BadToken,
    NUMBER,
    PLUS,
    MINUS,
    ASTERISK,
    SOLIDUS,
    LPAREN,
    RPAREN,
    WHITESPACE,
};

enum NodeType {
    BadNode,
    NUMBER_EXPR,
    BINARY_EXPR,
    UNARY_EXPR,
};

class Node;
class Token;

class Token {
public:

    string text;
    TokenType type;
    size_t pos;
    int val_int;
    bool is_null = true;

    Token() : type(TokenType::BadToken) { }
    Token(TokenType _type) : type(_type) { }

    string Type() const {
        if (type == TokenType::EOFToken) return "EOFToken";
        else if (type == TokenType::BadToken) return "InvalidToken";
        else if (type == TokenType::NUMBER) return "NUMBER";
        else if (type == TokenType::PLUS) return "PLUS";
        else if (type == TokenType::MINUS) return "MINUS";
        else if (type == TokenType::ASTERISK) return "ASTERISK";
        else if (type == TokenType::SOLIDUS) return "SOLIDUS";
        else if (type == TokenType::LPAREN) return "LPAREN";
        else if (type == TokenType::RPAREN) return "RPAREN";
        else if (type == TokenType::WHITESPACE) return "WHITESPACE";
        return "UnknownToken";
    }

    void operator=(const Token& token) {
        text = token.text;
        type = token.type;
        pos = token.pos;
        val_int = token.val_int;
        is_null = token.is_null;
    }

};

Node CreateNode(Token token);

class Node {
public:
    const NodeType type;
    Token token;
    virtual vector<Node> GetChildren() { return {CreateNode(token)}; }

    Node(NodeType _type) : type(_type) { }

    string Type() const {
        switch (type) {
            case NodeType::BadNode: return "InvalidNode";
            case NodeType::NUMBER_EXPR: return "NumberNode";
            case NodeType::BINARY_EXPR: return "BinaryExpression";
            case NodeType::UNARY_EXPR: return "UnaryExpression";
            default: return "UnknownNode";
        }
    }

    void operator=(const Node& node) { *this = node; }
    bool operator==(const Node& node) { return *this == node; }
};

Token CreateToken(TokenType type, size_t pos);

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
    SolidusToken(string _text, size_t _pos) : Token(TokenType::SOLIDUS) {
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
    InvalidNode(Token _token) : Node(NodeType::BadNode) {
        token = _token;
    }
};

class NumberNode : public Expression {
public:
    NumberNode(Token _token) : Expression(NodeType::NUMBER_EXPR) {
        token = _token;
    }

    vector<Node> GetChildren() { return {CreateNode(token)}; }
};

class BinaryExpression : public Expression {
public:
    Expression left;
    Token op;
    Expression right;

    BinaryExpression(Expression _left, Token _op, Expression _right) : Expression(NodeType::BINARY_EXPR), left(_left), op(_op), right(_right) { }
    vector<Node> GetChildren() { return {left, CreateNode(op), right}; }
};

class UnaryExpression : public Expression {

};

void PrettyPrint(Node node, string index="", bool last=false);

}

class Compiler {
private:

    Compiler() {}

public:

    static void compile() noexcept;

};

#endif
