using System.Collections.Generic;

namespace Buckle.CodeAnalysis {

    enum SyntaxType {
        Invalid,
        EOF,
        NUMBER,
        WHITESPACE,
        PLUS,
        MINUS,
        ASTERISK,
        SOLIDUS,
        LPAREN,
        RPAREN,
        NUMBER_EXPR,
        BINARY_EXPR,
        UNARY_EXPR,
        PAREN_EXPR,
    }

    abstract class Node {
        public abstract SyntaxType type { get; }
        public abstract List<Node> GetChildren();
    }

    class Token : Node {
        public override SyntaxType type { get; }
        public int pos { get; }
        public string text { get; }
        public object value { get; }

        public Token(SyntaxType type_, int pos_, string text_, object value_) {
            type = type_;
            pos = pos_;
            text = text_;
            value = value_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { }; }
    }

    abstract class Expression : Node { }

    class NumberNode : Expression {
        public Token token { get; }
        public override SyntaxType type => SyntaxType.NUMBER_EXPR;

        public NumberNode(Token token_) {
            token = token_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { token }; }
    }

    class BinaryExpression : Expression {
        public Expression left { get; }
        public Token op { get; }
        public Expression right { get; }
        public override SyntaxType type => SyntaxType.BINARY_EXPR;

        public BinaryExpression(Expression left_, Token op_, Expression right_) {
            left = left_;
            op = op_;
            right = right_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { left, op, right }; }
    }

    class ParenExpression : Expression {
        public Token lparen { get; }
        public Expression expr { get; }
        public Token rparen { get; }
        public override SyntaxType type => SyntaxType.PAREN_EXPR;

        public ParenExpression(Token lparen_, Expression expr_, Token rparen_) {
            lparen = lparen_;
            expr = expr_;
            rparen = rparen_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { lparen, expr, rparen }; }
    }

}
