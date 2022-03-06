/* Compiler entry point */
#pragma once
#ifndef COMPILER_HPP
#define COMPILER_HPP

#include "utils.hpp"

using std::cout;
using std::getline;
using std::cin;
using std::endl;

enum SyntaxTokenType {
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

class Token {
public:

    string text;
    const SyntaxTokenType type;
    size_t pos;
    int val_int;

    Token(SyntaxTokenType _type) : type(_type) {}

    string Type() {
        if (type == SyntaxTokenType::EOFToken) return "EOFToken";
        else if (type == SyntaxTokenType::BadToken) return "InvalidToken";
        else if (type == SyntaxTokenType::NUMBER) return "NUMBER";
        else if (type == SyntaxTokenType::PLUS) return "PLUS";
        else if (type == SyntaxTokenType::MINUS) return "MINUS";
        else if (type == SyntaxTokenType::ASTERISK) return "ASTERISK";
        else if (type == SyntaxTokenType::SOLIDUS) return "SOLIDUS";
        else if (type == SyntaxTokenType::LPAREN) return "LPAREN";
        else if (type == SyntaxTokenType::RPAREN) return "RPAREN";
        else if (type == SyntaxTokenType::WHITESPACE) return "WHITESPACE";
        return "UnknownToken";
    }

};

class EndOfFileToken : public Token {
public:
    EndOfFileToken(string _text, size_t _pos) : Token(SyntaxTokenType::EOFToken) {
        text = _text;
        pos = _pos;
    }
};

class NumberToken : public Token {
public:
    NumberToken(string _text, size_t _pos, int _value) : Token(SyntaxTokenType::NUMBER) {
        text = _text;
        pos = _pos;
        val_int = _value;
    }
};

class WhitespaceToken : public Token {
public:
    WhitespaceToken(string _text, size_t _pos) : Token(SyntaxTokenType::WHITESPACE) {
        text = _text;
        pos = _pos;
    }
};

class PlusToken : public Token {
public:
    PlusToken(string _text, size_t _pos) : Token(SyntaxTokenType::PLUS) {
        text = _text;
        pos = _pos;
    }
};

class MinusToken : public Token {
public:
    MinusToken(string _text, size_t _pos) : Token(SyntaxTokenType::MINUS) {
        text = _text;
        pos = _pos;
    }
};

class AsteriskToken : public Token {
public:
    AsteriskToken(string _text, size_t _pos) : Token(SyntaxTokenType::ASTERISK) {
        text = _text;
        pos = _pos;
    }
};

class SolidusToken : public Token {
public:
    SolidusToken(string _text, size_t _pos) : Token(SyntaxTokenType::SOLIDUS) {
        text = _text;
        pos = _pos;
    }
};

class LParenToken : public Token {
public:
    LParenToken(string _text, size_t _pos) : Token(SyntaxTokenType::LPAREN) {
        text = _text;
        pos = _pos;
    }
};

class RParenToken : public Token {
public:
    RParenToken(string _text, size_t _pos) : Token(SyntaxTokenType::RPAREN) {
        text = _text;
        pos = _pos;
    }
};

class InvalidToken : public Token {
public:
    InvalidToken(string _text, size_t _pos) : Token(SyntaxTokenType::BadToken) {
        text = _text;
        pos = _pos;
    }
};

class Compiler {
private:

    Compiler() {}

public:

    static void compile() noexcept;

};

#endif
