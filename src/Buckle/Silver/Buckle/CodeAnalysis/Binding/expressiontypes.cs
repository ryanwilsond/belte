using System;
using Buckle.CodeAnalysis.Symbols;
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
        public TypeSymbol leftType { get; }
        public TypeSymbol rightType { get; }
        public TypeSymbol resultType { get; }

        private BoundBinaryOperator(
            SyntaxType type_, BoundBinaryOperatorType opType_,
            TypeSymbol leftType_, TypeSymbol rightType_, TypeSymbol resultType_) {
            type = type_;
            opType = opType_;
            leftType = leftType_;
            rightType = rightType_;
            resultType = resultType_;
        }

        private BoundBinaryOperator(
            SyntaxType type, BoundBinaryOperatorType opType, TypeSymbol operandType, TypeSymbol resultType)
            : this(type, opType, operandType, operandType, resultType) { }

        private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, TypeSymbol lType)
            : this(type, opType, lType, lType, lType) { }

        private static BoundBinaryOperator[] operators_ = {
            new BoundBinaryOperator(SyntaxType.PLUS, BoundBinaryOperatorType.Addition, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.MINUS, BoundBinaryOperatorType.Subtraction, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.ASTERISK, BoundBinaryOperatorType.Multiplication, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.SLASH, BoundBinaryOperatorType.Division, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.DASTERISK, BoundBinaryOperatorType.Power, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.AMPERSAND, BoundBinaryOperatorType.LogicalAnd, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.PIPE, BoundBinaryOperatorType.LogicalOr, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.CARET, BoundBinaryOperatorType.LogicalXor, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.SHIFTLEFT, BoundBinaryOperatorType.LeftShift, TypeSymbol.Int),
            new BoundBinaryOperator(SyntaxType.SHIFTRIGHT, BoundBinaryOperatorType.RightShift, TypeSymbol.Int),

            new BoundBinaryOperator(
                SyntaxType.DEQUALS, BoundBinaryOperatorType.EqualityEquals, TypeSymbol.Int, TypeSymbol.Bool),
            new BoundBinaryOperator(
                SyntaxType.BANGEQUALS, BoundBinaryOperatorType.EqualityNotEquals, TypeSymbol.Int, TypeSymbol.Bool),
            new BoundBinaryOperator(
                SyntaxType.LANGLEBRACKET, BoundBinaryOperatorType.LessThan, TypeSymbol.Int, TypeSymbol.Bool),
            new BoundBinaryOperator(
                SyntaxType.RANGLEBRACKET, BoundBinaryOperatorType.GreaterThan, TypeSymbol.Int, TypeSymbol.Bool),
            new BoundBinaryOperator(
                SyntaxType.LESSEQUAL, BoundBinaryOperatorType.LessOrEqual, TypeSymbol.Int, TypeSymbol.Bool),
            new BoundBinaryOperator(
                SyntaxType.GREATEQUAL, BoundBinaryOperatorType.GreatOrEqual, TypeSymbol.Int, TypeSymbol.Bool),

            new BoundBinaryOperator(SyntaxType.DAMPERSAND, BoundBinaryOperatorType.ConditionalAnd, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.DPIPE, BoundBinaryOperatorType.ConditionalOr, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.AMPERSAND, BoundBinaryOperatorType.LogicalAnd, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.PIPE, BoundBinaryOperatorType.LogicalOr, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.CARET, BoundBinaryOperatorType.LogicalXor, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.DEQUALS, BoundBinaryOperatorType.EqualityEquals, TypeSymbol.Bool),
            new BoundBinaryOperator(SyntaxType.BANGEQUALS, BoundBinaryOperatorType.EqualityNotEquals, TypeSymbol.Bool),

            new BoundBinaryOperator(SyntaxType.PLUS, BoundBinaryOperatorType.Addition, TypeSymbol.String),
        };

        public static BoundBinaryOperator Bind(SyntaxType type, TypeSymbol leftType, TypeSymbol rightType) {
            foreach (var op in operators_)
                if (op.type == type && op.leftType == leftType && op.rightType == rightType) return op;

            return null;
        }
    }

    internal sealed class BoundBinaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.BinaryExpression;
        public override TypeSymbol lType => op.resultType;
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
        public TypeSymbol operandType { get; }
        public TypeSymbol resultType { get; }

        private BoundUnaryOperator(
            SyntaxType type_, BoundUnaryOperatorType opType_, TypeSymbol operandType_, TypeSymbol resultType_) {
            type = type_;
            opType = opType_;
            operandType = operandType_;
            resultType = resultType_;
        }

        private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, TypeSymbol operandType)
            : this(type, opType, operandType, operandType) { }

        private static BoundUnaryOperator[] operators_ = {
            new BoundUnaryOperator(SyntaxType.BANG, BoundUnaryOperatorType.BooleanNegation, TypeSymbol.Bool),

            new BoundUnaryOperator(SyntaxType.PLUS, BoundUnaryOperatorType.NumericalIdentity, TypeSymbol.Int),
            new BoundUnaryOperator(SyntaxType.MINUS, BoundUnaryOperatorType.NumericalNegation, TypeSymbol.Int),
            new BoundUnaryOperator(SyntaxType.TILDE, BoundUnaryOperatorType.BitwiseCompliment, TypeSymbol.Int),
        };

        public static BoundUnaryOperator Bind(SyntaxType type, TypeSymbol operandType) {
            foreach (var op in operators_)
                if (op.type == type && op.operandType == operandType) return op;

            return null;
        }
    }

    internal sealed class BoundUnaryExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.UnaryExpression;
        public override TypeSymbol lType => op.resultType;
        public BoundUnaryOperator op { get; }
        public BoundExpression operand { get; }

        public BoundUnaryExpression(BoundUnaryOperator op_, BoundExpression operand_) {
            op = op_;
            operand = operand_;
        }
    }

    internal abstract class BoundExpression : BoundNode {
        public abstract TypeSymbol lType { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LiteralExpression;
        public override TypeSymbol lType { get; }
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;

            if (value is bool)
                lType = TypeSymbol.Bool;
            else if (value is int)
                lType = TypeSymbol.Int;
            else if (value is string)
                lType = TypeSymbol.String;
            else
                throw new Exception($"unexpected literal '{value}' of type '{value.GetType()}'");
        }
    }

    internal sealed class BoundVariableExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public override TypeSymbol lType => variable.lType;
        public override BoundNodeType type => BoundNodeType.VariableExpression;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expression { get; }
        public override BoundNodeType type => BoundNodeType.AssignmentExpression;
        public override TypeSymbol lType => expression.lType;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
            variable = variable_;
            expression = expression_;
        }
    }

    internal sealed class BoundEmptyExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.EmptyExpression;
        public override TypeSymbol lType => null;

        public BoundEmptyExpression() { }
    }

    internal sealed class BoundErrorExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.ErrorExpression;
        public override TypeSymbol lType => TypeSymbol.Error;

        public BoundErrorExpression() { }
    }
}
