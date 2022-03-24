using System;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UNARY_EXPR,
        LITERAL_EXPR,
        BINARY_EXPR,
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
}
