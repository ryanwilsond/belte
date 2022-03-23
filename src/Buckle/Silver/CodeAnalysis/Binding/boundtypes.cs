using System;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UNARY_EXPR,
        LITERAL_EXPR,
        BINARY_EXPR,
    }

    internal enum BoundUnaryOperatorType {
        Invalid,
        Identity,
        Negation,
    }

    internal enum BoundBinaryOperatorType {
        Invalid,
        Add,
        Subtract,
        Multiply,
        Divide,
    }

    internal abstract class BoundNode {
        public abstract BoundNodeType type { get; }
    }

    internal abstract class BoundExpression : BoundNode {
        public abstract Type ltype { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LITERAL_EXPR;
        public override Type ltype => value.GetType();
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;
        }
    }

    internal sealed class BoundUnaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.UNARY_EXPR;
        public override Type ltype => operand.ltype;
        public BoundUnaryOperatorType op { get; }
        public BoundExpression operand { get; }

        public BoundUnaryExpression(BoundUnaryOperatorType op_, BoundExpression operand_) {
            op = op_;
            operand = operand_;
        }
    }

    internal sealed class BoundBinaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.BINARY_EXPR;
        public override Type ltype => left.ltype;
        public BoundExpression left { get; }
        public BoundBinaryOperatorType op { get; }
        public BoundExpression right { get; }

        public BoundBinaryExpression(BoundExpression left_, BoundBinaryOperatorType op_, BoundExpression right_) {
            left = left_;
            op = op_;
            right = right_;
        }
    }
}
