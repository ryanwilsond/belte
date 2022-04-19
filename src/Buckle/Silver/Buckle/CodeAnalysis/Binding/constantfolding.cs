using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding {
    internal static class ConstantFolding {
        public static BoundConstant ComputeConstant(
            BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
            var leftConstant = left.constantValue;
            var rightConstant = right.constantValue;

            // and/or allow one side to be null
            // TODO: track statements with side effects (e.g. function calls) and still execute them left ro right
            if (op.opType == BoundBinaryOperatorType.ConditionalAnd) {
                if (leftConstant != null && !(bool)leftConstant.value ||
                    rightConstant != null && !(bool)rightConstant.value)
                    return new BoundConstant(false);
            }

            if (op.opType == BoundBinaryOperatorType.ConditionalOr) {
                if (leftConstant != null && (bool)leftConstant.value ||
                    rightConstant != null && (bool)rightConstant.value)
                    return new BoundConstant(true);
            }

            if (leftConstant == null || rightConstant == null)
                return null;

            var leftValue = leftConstant.value;
            var rightValue = rightConstant.value;

            switch (op.opType) {
                case BoundBinaryOperatorType.Addition:
                    if (left.lType == TypeSymbol.Int)
                        return new BoundConstant((int)leftValue + (int)rightValue);
                    else
                        return new BoundConstant((string)leftValue + (string)rightValue);
                case BoundBinaryOperatorType.Subtraction: return new BoundConstant((int)leftValue - (int)rightValue);
                case BoundBinaryOperatorType.Multiplication: return new BoundConstant((int)leftValue * (int)rightValue);
                case BoundBinaryOperatorType.Division: return new BoundConstant((int)leftValue / (int)rightValue);
                case BoundBinaryOperatorType.Power: return new BoundConstant((int)Math.Pow((int)leftValue, (int)rightValue));
                case BoundBinaryOperatorType.ConditionalAnd: return new BoundConstant((bool)leftValue && (bool)rightValue);
                case BoundBinaryOperatorType.ConditionalOr: return new BoundConstant((bool)leftValue || (bool)rightValue);
                case BoundBinaryOperatorType.EqualityEquals: return new BoundConstant(Equals(leftValue, rightValue));
                case BoundBinaryOperatorType.EqualityNotEquals: return new BoundConstant(!Equals(leftValue, rightValue));
                case BoundBinaryOperatorType.LessThan: return new BoundConstant((int)leftValue < (int)rightValue);
                case BoundBinaryOperatorType.GreaterThan: return new BoundConstant((int)leftValue > (int)rightValue);
                case BoundBinaryOperatorType.LessOrEqual: return new BoundConstant((int)leftValue <= (int)rightValue);
                case BoundBinaryOperatorType.GreatOrEqual: return new BoundConstant((int)leftValue >= (int)rightValue);
                case BoundBinaryOperatorType.LogicalAnd:
                    if (left.lType == TypeSymbol.Int)
                        return new BoundConstant((int)leftValue & (int)rightValue);
                    else
                        return new BoundConstant((bool)leftValue & (bool)rightValue);
                case BoundBinaryOperatorType.LogicalOr:
                    if (left.lType == TypeSymbol.Int)
                        return new BoundConstant((int)leftValue | (int)rightValue);
                    else
                        return new BoundConstant((bool)leftValue | (bool)rightValue);
                case BoundBinaryOperatorType.LogicalXor:
                    if (left.lType == TypeSymbol.Int)
                        return new BoundConstant((int)leftValue ^ (int)rightValue);
                    else
                        return new BoundConstant((bool)leftValue ^ (bool)rightValue);
                case BoundBinaryOperatorType.LeftShift: return new BoundConstant((int)leftValue << (int)rightValue);
                case BoundBinaryOperatorType.RightShift: return new BoundConstant((int)leftValue >> (int)rightValue);
                default:
                    throw new Exception($"unexpected binary operator {op.opType}");
            }
        }

        public static BoundConstant ComputeConstant(BoundUnaryOperator op, BoundExpression operand) {
            if (operand.constantValue != null && operand.constantValue.value is int value) {
                switch (op.opType) {
                    case BoundUnaryOperatorType.NumericalIdentity:
                        return new BoundConstant((int)operand.constantValue.value);
                    case BoundUnaryOperatorType.NumericalNegation:
                        return new BoundConstant(-(int)operand.constantValue.value);
                    case BoundUnaryOperatorType.BooleanNegation:
                        return new BoundConstant(!(bool)operand.constantValue.value);
                    case BoundUnaryOperatorType.BitwiseCompliment:
                        return new BoundConstant(~(int)operand.constantValue.value);
                    default:
                        throw new Exception($"unexpected unary operator {op.opType}");
                }
            }

            return null;
        }
    }
}
