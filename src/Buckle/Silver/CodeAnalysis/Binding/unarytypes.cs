using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundUnaryOperatorType {
        Invalid,
        NumericalIdentity,
        NumericalNegation,
        BooleanNegation,
    }

    internal class BoundUnaryOperator {
        public SyntaxType type { get; }
        public BoundUnaryOperatorType optype { get; }
        public Type operandtype { get; }
        public Type resulttype { get; }

        private BoundUnaryOperator(SyntaxType type_, BoundUnaryOperatorType optype_, Type operandtype_, Type resulttype_) {
            type = type_;
            optype = optype_;
            operandtype = operandtype_;
            resulttype = resulttype_;
        }

        private BoundUnaryOperator(SyntaxType type_, BoundUnaryOperatorType optype_, Type operandtype_)
            : this(type_, optype_, operandtype_, operandtype_) { }

        private static BoundUnaryOperator[] operators_ = {
            new BoundUnaryOperator(SyntaxType.BANG, BoundUnaryOperatorType.BooleanNegation, typeof(bool)),
            new BoundUnaryOperator(SyntaxType.PLUS, BoundUnaryOperatorType.NumericalIdentity, typeof(bool)),
            new BoundUnaryOperator(SyntaxType.MINUS, BoundUnaryOperatorType.NumericalNegation, typeof(bool)),
        };

        public static BoundUnaryOperator Bind(SyntaxType type, Type operandtype) {
            foreach (var op in operators_)
                if (op.type == type && op.operandtype == operandtype) return op;

            return null;
        }
    }

    internal sealed class BoundUnaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.UNARY_EXPR;
        public override Type ltype => op.resulttype;
        public BoundUnaryOperator op { get; }
        public BoundExpression operand { get; }

        public BoundUnaryExpression(BoundUnaryOperator op_, BoundExpression operand_) {
            op = op_;
            operand = operand_;
        }
    }
}
