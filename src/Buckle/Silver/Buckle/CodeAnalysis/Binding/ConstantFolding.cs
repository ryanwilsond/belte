using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal static class ConstantFolding {
    public static BoundConstant Fold(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        // and/or allow one side to be null
        // TODO: track statements with side effects (e.g. function calls) and still execute them left to right
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

        if (left.lType == TypeSymbol.Bool) {
            leftValue = Convert.ToBoolean(leftValue);
            rightValue = Convert.ToBoolean(rightValue);
        } else if (left.lType == TypeSymbol.Int) {
            leftValue = Convert.ToInt32(leftValue);
            rightValue = Convert.ToInt32(rightValue);
        } else if (left.lType == TypeSymbol.Decimal) {
            leftValue = Convert.ToSingle(leftValue);
            rightValue = Convert.ToSingle(rightValue);
        } else if (left.lType == TypeSymbol.String) {
            leftValue = Convert.ToString(leftValue);
            rightValue = Convert.ToString(rightValue);
        }

        switch (op.opType) {case BoundBinaryOperatorType.Addition:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue + (int)rightValue);
                else if (left.lType == TypeSymbol.String)
                    return new BoundConstant((string)leftValue + (string)rightValue);
                else
                    return new BoundConstant((float)leftValue + (float)rightValue);
            case BoundBinaryOperatorType.Subtraction:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue - (int)rightValue);
                else
                    return new BoundConstant((float)leftValue - (float)rightValue);
            case BoundBinaryOperatorType.Multiplication:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue * (int)rightValue);
                else
                    return new BoundConstant((float)leftValue * (float)rightValue);
            case BoundBinaryOperatorType.Division:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue / (int)rightValue);
                else
                    return new BoundConstant((float)leftValue / (float)rightValue);
            case BoundBinaryOperatorType.Power:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new BoundConstant((float)Math.Pow((float)leftValue, (float)rightValue));
            case BoundBinaryOperatorType.ConditionalAnd:
                return new BoundConstant((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorType.ConditionalOr:
                return new BoundConstant((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorType.EqualityEquals:
                return new BoundConstant(Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.EqualityNotEquals:
                return new BoundConstant(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.LessThan:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue < (int)rightValue);
                else
                    return new BoundConstant((float)leftValue < (float)rightValue);
            case BoundBinaryOperatorType.GreaterThan:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue > (int)rightValue);
                else
                    return new BoundConstant((float)leftValue > (float)rightValue);
            case BoundBinaryOperatorType.LessOrEqual:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue <= (int)rightValue);
                else
                    return new BoundConstant((float)leftValue <= (float)rightValue);
            case BoundBinaryOperatorType.GreatOrEqual:
                if (left.lType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue >= (int)rightValue);
                else
                    return new BoundConstant((float)leftValue >= (float)rightValue);
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
            case BoundBinaryOperatorType.LeftShift:
                return new BoundConstant((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorType.RightShift:
                return new BoundConstant((int)leftValue >> (int)rightValue);
            default:
                throw new Exception($"unexpected binary operator '{op.opType}'");
        }
    }

    public static BoundConstant ComputeConstant(BoundUnaryOperator op, BoundExpression operand) {
        if (operand.constantValue != null && operand.constantValue.value is int value) {
            switch (op.opType) {
                case BoundUnaryOperatorType.NumericalIdentity:
                    if (operand.lType == TypeSymbol.Int)
                        return new BoundConstant((int)operand.constantValue.value);
                    else
                        return new BoundConstant((float)operand.constantValue.value);
                case BoundUnaryOperatorType.NumericalNegation:
                    if (operand.lType == TypeSymbol.Int)
                        return new BoundConstant(-(int)operand.constantValue.value);
                    else
                        return new BoundConstant(-(float)operand.constantValue.value);
                case BoundUnaryOperatorType.BooleanNegation:
                    return new BoundConstant(!(bool)operand.constantValue.value);
                case BoundUnaryOperatorType.BitwiseCompliment:
                    return new BoundConstant(~(int)operand.constantValue.value);
                default:
                    throw new Exception($"unexpected unary operator '{op.opType}'");
            }
        }

        return null;
    }
}
