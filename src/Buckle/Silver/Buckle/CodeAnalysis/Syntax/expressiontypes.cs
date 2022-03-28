
namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class Expression : Node { }

    internal sealed class LiteralExpression : Expression {
        public Token token { get; }
        public object value { get; }
        public override SyntaxType type => SyntaxType.LITERAL_EXPR;

        public LiteralExpression(Token token_, object value_) {
            token = token_;
            value = value_;
        }

        public LiteralExpression(Token token_) : this(token_, token_.value) { }
    }

    internal sealed class BinaryExpression : Expression {
        public Expression left { get; }
        public Token op { get; }
        public Expression right { get; }
        public override SyntaxType type => SyntaxType.BINARY_EXPR;

        public BinaryExpression(Expression left_, Token op_, Expression right_) {
            left = left_;
            op = op_;
            right = right_;
        }
    }

    internal sealed class ParenExpression : Expression {
        public Token lparen { get; }
        public Expression expr { get; }
        public Token rparen { get; }
        public override SyntaxType type => SyntaxType.PAREN_EXPR;

        public ParenExpression(Token lparen_, Expression expr_, Token rparen_) {
            lparen = lparen_;
            expr = expr_;
            rparen = rparen_;
        }
    }

    internal sealed class UnaryExpression : Expression {
        public Token op { get; }
        public Expression operand { get; }
        public override SyntaxType type => SyntaxType.UNARY_EXPR;

        public UnaryExpression(Token op_, Expression operand_) {
            op = op_;
            operand = operand_;
        }
    }

    internal sealed class NameExpression : Expression {
        public Token id { get; }
        public override SyntaxType type => SyntaxType.NAME_EXPR;

        public NameExpression(Token id_) {
            id = id_;
        }
    }

    internal sealed class AssignmentExpression : Expression {
        public Token id { get; }
        public Token equals { get; }
        public Expression expr { get; }
        public override SyntaxType type => SyntaxType.ASSIGN_EXPR;

        public AssignmentExpression(Token id_, Token equals_, Expression expr_) {
            id = id_;
            equals = equals_;
            expr = expr_;
        }
    }
}
