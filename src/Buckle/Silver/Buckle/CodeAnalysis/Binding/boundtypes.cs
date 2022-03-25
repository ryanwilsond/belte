using System;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UNARY_EXPR,
        LITERAL_EXPR,
        BINARY_EXPR,
        VARIABLE_EXPR,
        ASSIGN_EXPR,
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

    internal sealed class BoundVariableExpression : BoundExpression {
        public string name { get; }
        public override Type ltype { get; }
        public override BoundNodeType type => BoundNodeType.VARIABLE_EXPR;

        public BoundVariableExpression(string name_, Type ltype_) {
            name = name_;
            ltype = ltype_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public string name { get; }
        public BoundExpression expr { get; }
        public override BoundNodeType type => BoundNodeType.ASSIGN_EXPR;
        public override Type ltype => expr.ltype;

        public BoundAssignmentExpression(string name_, BoundExpression expr_) {
            name = name_;
            expr = expr_;
        }
    }
}
