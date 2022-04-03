using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundBinaryOperatorType {
        Invalid,
        Addition,
        Subtraction,
        Multiplication,
        Division,
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
            new BoundBinaryOperator(SyntaxType.PLUS, BoundBinaryOperatorType.Addition, typeof(int)),
            new BoundBinaryOperator(SyntaxType.MINUS, BoundBinaryOperatorType.Subtraction, typeof(int)),
            new BoundBinaryOperator(SyntaxType.ASTERISK, BoundBinaryOperatorType.Multiplication, typeof(int)),
            new BoundBinaryOperator(SyntaxType.SLASH, BoundBinaryOperatorType.Division, typeof(int)),
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

    internal enum BoundUnaryOperatorType {
        Invalid,
        NumericalIdentity,
        NumericalNegation,
        BooleanNegation,
        BitwiseCompliment,
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
            new BoundUnaryOperator(SyntaxType.TILDE, BoundUnaryOperatorType.BitwiseCompliment, typeof(int)),
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

    internal abstract class BoundExpression : BoundNode {
        public abstract Type lType { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LiteralExpression;
        public override Type lType => value.GetType();
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;
        }
    }

    internal sealed class BoundVariableExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public override Type lType => variable.lType;
        public override BoundNodeType type => BoundNodeType.VariableExpression;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expression { get; }
        public override BoundNodeType type => BoundNodeType.AssignmentExpression;
        public override Type lType => expression.lType;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
            variable = variable_;
            expression = expression_;
        }
    }

    internal sealed class BoundEmptyExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.EmptyExpression;
        public override Type lType => null;

        public BoundEmptyExpression() { }
    }
}
