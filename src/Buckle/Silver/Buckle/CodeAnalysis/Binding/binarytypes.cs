using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundBinaryOperatorType {
        Invalid,
        Add,
        Subtract,
        Multiply,
        Divide,
        Power,
        LogicalAnd,
        LogicalOr,
        LogicalXor,
        LeftShift,
        RightShift,
        ConditionalAnd,
        ConditionalOr,
        EqualityEquals,
        EqualityNotEquals,
        LessThan,
        GreaterThan,
        LessOrEqual,
        GreatOrEqual,
    }

    internal sealed class BoundBinaryOperator {
        public SyntaxType type { get; }
        public BoundBinaryOperatorType opType { get; }
        public Type leftType { get; }
        public Type rightType { get; }
        public Type resultType { get; }

        private BoundBinaryOperator(
            SyntaxType type_, BoundBinaryOperatorType opType_, Type leftType_, Type rightType_, Type resultType_) {
            type = type_;
            opType = opType_;
            leftType = leftType_;
            rightType = rightType_;
            resultType = resultType_;
        }

        private BoundBinaryOperator(
            SyntaxType type, BoundBinaryOperatorType opType, Type operandType, Type resultType)
            : this(type, opType, operandType, operandType, resultType) { }

        private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, Type lType)
            : this(type, opType, lType, lType, lType) { }

        private static BoundBinaryOperator[] operators_ = {
            new BoundBinaryOperator(SyntaxType.PLUS, BoundBinaryOperatorType.Add, typeof(int)),
            new BoundBinaryOperator(SyntaxType.MINUS, BoundBinaryOperatorType.Subtract, typeof(int)),
            new BoundBinaryOperator(SyntaxType.ASTERISK, BoundBinaryOperatorType.Multiply, typeof(int)),
            new BoundBinaryOperator(SyntaxType.SLASH, BoundBinaryOperatorType.Divide, typeof(int)),
            new BoundBinaryOperator(SyntaxType.DASTERISK, BoundBinaryOperatorType.Power, typeof(int)),
            new BoundBinaryOperator(SyntaxType.AMPERSAND, BoundBinaryOperatorType.LogicalAnd, typeof(int)),
            new BoundBinaryOperator(SyntaxType.PIPE, BoundBinaryOperatorType.LogicalOr, typeof(int)),
            new BoundBinaryOperator(SyntaxType.CARET, BoundBinaryOperatorType.LogicalXor, typeof(int)),
            new BoundBinaryOperator(SyntaxType.SHIFTLEFT, BoundBinaryOperatorType.LeftShift, typeof(int)),
            new BoundBinaryOperator(SyntaxType.SHIFTRIGHT, BoundBinaryOperatorType.RightShift, typeof(int)),

            new BoundBinaryOperator(
                SyntaxType.DEQUALS, BoundBinaryOperatorType.EqualityEquals, typeof(int), typeof(bool)),
            new BoundBinaryOperator(
                SyntaxType.BANGEQUALS, BoundBinaryOperatorType.EqualityNotEquals, typeof(int), typeof(bool)),
            new BoundBinaryOperator(
                SyntaxType.LANGLEBRACKET, BoundBinaryOperatorType.LessThan, typeof(int), typeof(bool)),
            new BoundBinaryOperator(
                SyntaxType.RANGLEBRACKET, BoundBinaryOperatorType.GreaterThan, typeof(int), typeof(bool)),
            new BoundBinaryOperator(
                SyntaxType.LESSEQUAL, BoundBinaryOperatorType.LessOrEqual, typeof(int), typeof(bool)),
            new BoundBinaryOperator(
                SyntaxType.GREATEQUAL, BoundBinaryOperatorType.GreatOrEqual, typeof(int), typeof(bool)),

            new BoundBinaryOperator(SyntaxType.DAMPERSAND, BoundBinaryOperatorType.ConditionalAnd, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.DPIPE, BoundBinaryOperatorType.ConditionalOr, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.AMPERSAND, BoundBinaryOperatorType.LogicalAnd, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.PIPE, BoundBinaryOperatorType.LogicalOr, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.CARET, BoundBinaryOperatorType.LogicalXor, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.DEQUALS, BoundBinaryOperatorType.EqualityEquals, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.BANGEQUALS, BoundBinaryOperatorType.EqualityNotEquals, typeof(bool)),
        };

        public static BoundBinaryOperator Bind(SyntaxType type, Type leftType, Type rightType) {
            foreach (var op in operators_)
                if (op.type == type && op.leftType == leftType && op.rightType == rightType) return op;

            return null;
        }
    }

    internal sealed class BoundBinaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.BinaryExpression;
        public override Type lType => op.resultType;
        public BoundExpression left { get; }
        public BoundBinaryOperator op { get; }
        public BoundExpression right { get; }

        public BoundBinaryExpression(BoundExpression left_, BoundBinaryOperator op_, BoundExpression right_) {
            left = left_;
            op = op_;
            right = right_;
        }
    }
}
