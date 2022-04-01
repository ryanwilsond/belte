using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundUnaryOperatorType {
        Invalid,
        NumericalIdentity,
        NumericalNegation,
        BooleanNegation,
    }

    internal sealed class BoundUnaryOperator {
        public SyntaxType type { get; }
        public BoundUnaryOperatorType opType { get; }
        public Type operandType { get; }
        public Type resultType { get; }

        private BoundUnaryOperator(
            SyntaxType type_, BoundUnaryOperatorType opType_, Type operandType_, Type resultType_) {
            type = type_;
            opType = opType_;
            operandType = operandType_;
            resultType = resultType_;
        }

        private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, Type operandType)
            : this(type, opType, operandType, operandType) { }

        private static BoundUnaryOperator[] operators_ = {
            new BoundUnaryOperator(SyntaxType.BANG, BoundUnaryOperatorType.BooleanNegation, typeof(bool)),

            new BoundUnaryOperator(SyntaxType.PLUS, BoundUnaryOperatorType.NumericalIdentity, typeof(int)),
            new BoundUnaryOperator(SyntaxType.MINUS, BoundUnaryOperatorType.NumericalNegation, typeof(int)),
        };

        public static BoundUnaryOperator Bind(SyntaxType type, Type operandType) {
            foreach (var op in operators_)
                if (op.type == type && op.operandType == operandType) return op;

            return null;
        }
    }

    internal sealed class BoundUnaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.UnaryExpression;
        public override Type lType => op.resultType;
        public BoundUnaryOperator op { get; }
        public BoundExpression operand { get; }

        public BoundUnaryExpression(BoundUnaryOperator op_, BoundExpression operand_) {
            op = op_;
            operand = operand_;
        }
    }
}
