using System.Collections.Generic;

namespace Buckle.CodeAnalysis {

    internal enum SyntaxType {
        Invalid,
        // tokens
        EOF,
        WHITESPACE,
        NUMBER,
        PLUS,
        MINUS,
        ASTERISK,
        SOLIDUS,
        LPAREN,
        RPAREN,
        // expressions
        NUMBER_EXPR,
        BINARY_EXPR,
        UNARY_EXPR,
        PAREN_EXPR,
    }

    internal abstract class Node {
        public abstract SyntaxType type { get; }
        public abstract List<Node> GetChildren();
    }

    internal class Token : Node {
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

    internal abstract class Expression : Node { }

    internal class NumberNode : Expression {
        public Token token { get; }
        public override SyntaxType type => SyntaxType.NUMBER_EXPR;

        public NumberNode(Token token_) {
            token = token_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { token }; }
    }

    internal class BinaryExpression : Expression {
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

    internal class ParenExpression : Expression {
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

    internal class UnaryExpression : Expression {
        public Token op { get; }
        public Expression operand { get; }
        public override SyntaxType type => SyntaxType.UNARY_EXPR;

        public UnaryExpression(Token op_, Expression operand_) {
            op = op_;
            operand = operand_;
        }

        public override List<Node> GetChildren() { return new List<Node>() { op, operand }; }
    }

}
