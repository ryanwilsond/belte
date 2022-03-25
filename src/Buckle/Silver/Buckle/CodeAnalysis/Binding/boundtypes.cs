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
        public VariableSymbol variable { get; }
        public override Type ltype => variable.ltype;
        public override BoundNodeType type => BoundNodeType.VARIABLE_EXPR;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expr { get; }
        public override BoundNodeType type => BoundNodeType.ASSIGN_EXPR;
        public override Type ltype => expr.ltype;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expr_) {
            variable = variable_;
            expr = expr_;
        }
    }
}
