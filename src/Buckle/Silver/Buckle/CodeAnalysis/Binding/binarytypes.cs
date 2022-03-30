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
        public BoundBinaryOperatorType optype { get; }
        public Type lefttype { get; }
        public Type righttype { get; }
        public Type resulttype { get; }

        private BoundBinaryOperator(
            SyntaxType type_, BoundBinaryOperatorType optype_, Type lefttype_, Type righttype_, Type resulttype_) {
            type = type_;
            optype = optype_;
            lefttype = lefttype_;
            righttype = righttype_;
            resulttype = resulttype_;
        }

        private BoundBinaryOperator(
            SyntaxType type_, BoundBinaryOperatorType optype_, Type operandtype, Type resulttype_)
            : this(type_, optype_, operandtype, operandtype, resulttype_) { }

        private BoundBinaryOperator(SyntaxType type_, BoundBinaryOperatorType optype_, Type ltype)
            : this(type_, optype_, ltype, ltype, ltype) { }

        private static BoundBinaryOperator[] operators_ = {
            new BoundBinaryOperator(SyntaxType.PLUS, BoundBinaryOperatorType.Add, typeof(int)),
            new BoundBinaryOperator(SyntaxType.MINUS, BoundBinaryOperatorType.Subtract, typeof(int)),
            new BoundBinaryOperator(SyntaxType.ASTERISK, BoundBinaryOperatorType.Multiply, typeof(int)),
            new BoundBinaryOperator(SyntaxType.SOLIDUS, BoundBinaryOperatorType.Divide, typeof(int)),
            new BoundBinaryOperator(SyntaxType.DASTERISK, BoundBinaryOperatorType.Power, typeof(int)),

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
            new BoundBinaryOperator(SyntaxType.DEQUALS, BoundBinaryOperatorType.EqualityEquals, typeof(bool)),
            new BoundBinaryOperator(SyntaxType.BANGEQUALS, BoundBinaryOperatorType.EqualityNotEquals, typeof(bool)),
        };

        public static BoundBinaryOperator Bind(SyntaxType type, Type lefttype, Type righttype) {
            foreach (var op in operators_)
                if (op.type == type && op.lefttype == lefttype && op.righttype == righttype) return op;

            return null;
        }
    }

    internal sealed class BoundBinaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.BINARY_EXPR;
        public override Type ltype => op.resulttype;
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
