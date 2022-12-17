using System;
using System.Numerics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Folds/evaluates simple BoundExpressions during compile time.
/// </summary>
internal static class ConstantFolding {
    /// <summary>
    /// Folds a <see cref="BinaryExpression" /> (if possible).
    /// </summary>
    /// <param name="left">Left side operand.</param>
    /// <param name="op">Operator.</param>
    /// <param name="right">Right side operand.</param>
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant Fold(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        // With and/or operators allow one side to be null
        // TODO Track statements with side effects (e.g. function calls) and still execute them left to right
        if (op?.opType == BoundBinaryOperatorType.ConditionalAnd) {
            if ((leftConstant != null && leftConstant.value != null && !(bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && !(bool)rightConstant.value))
                return new BoundConstant(false);
        }

        if (op?.opType == BoundBinaryOperatorType.ConditionalOr) {
            if ((leftConstant != null && leftConstant.value != null && (bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && (bool)rightConstant.value))
                return new BoundConstant(true);
        }

        if (leftConstant == null || rightConstant == null || op == null)
            return null;

        var leftValue = leftConstant.value;
        var rightValue = rightConstant.value;
        var leftType = op.leftType.lType;
        var rightType = op.rightType.lType;

        if (leftValue == null || rightValue == null)
            return new BoundConstant(null);

        if (leftType == TypeSymbol.Bool) {
            leftValue = Convert.ToBoolean(leftValue);
            rightValue = Convert.ToBoolean(rightValue);
        } else if (leftType == TypeSymbol.Int) {
            // Prevents bankers rounding from Convert.ToInt32, instead always truncate (no rounding)
            if (leftValue.IsFloatingPoint())
                leftValue = Math.Truncate(Convert.ToDouble(leftValue));
            if (rightValue.IsFloatingPoint())
                rightValue = Math.Truncate(Convert.ToDouble(rightValue));

            leftValue = Convert.ToInt32(leftValue);
            rightValue = Convert.ToInt32(rightValue);
        } else if (leftType == TypeSymbol.Decimal) {
            leftValue = Convert.ToDouble(leftValue);
            rightValue = Convert.ToDouble(rightValue);
        } else if (leftType == TypeSymbol.String) {
            leftValue = Convert.ToString(leftValue);
            rightValue = Convert.ToString(rightValue);
        }

        switch (op.opType) {
            case BoundBinaryOperatorType.Addition:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue + (int)rightValue);
                else if (leftType == TypeSymbol.String)
                    return new BoundConstant((string)leftValue + (string)rightValue);
                else
                    return new BoundConstant((double)leftValue + (double)rightValue);
            case BoundBinaryOperatorType.Subtraction:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue - (int)rightValue);
                else
                    return new BoundConstant((double)leftValue - (double)rightValue);
            case BoundBinaryOperatorType.Multiplication:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue * (int)rightValue);
                else
                    return new BoundConstant((double)leftValue * (double)rightValue);
            case BoundBinaryOperatorType.Division:
                if (leftType == TypeSymbol.Int) {
                    if ((int)rightValue != 0)
                        return new BoundConstant((int)leftValue / (int)rightValue);
                } else {
                    if ((double)rightValue != 0)
                        return new BoundConstant((double)leftValue / (double)rightValue);
                }

                return null;
            case BoundBinaryOperatorType.Power:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new BoundConstant((double)Math.Pow((double)leftValue, (double)rightValue));
            case BoundBinaryOperatorType.ConditionalAnd:
                return new BoundConstant((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorType.ConditionalOr:
                return new BoundConstant((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorType.EqualityEquals:
                return new BoundConstant(Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.EqualityNotEquals:
                return new BoundConstant(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue < (int)rightValue);
                else
                    return new BoundConstant((double)leftValue < (double)rightValue);
            case BoundBinaryOperatorType.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue > (int)rightValue);
                else
                    return new BoundConstant((double)leftValue > (double)rightValue);
            case BoundBinaryOperatorType.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue <= (int)rightValue);
                else
                    return new BoundConstant((double)leftValue <= (double)rightValue);
            case BoundBinaryOperatorType.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue >= (int)rightValue);
                else
                    return new BoundConstant((double)leftValue >= (double)rightValue);
            case BoundBinaryOperatorType.LogicalAnd:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue & (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorType.LogicalOr:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue | (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorType.LogicalXor:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue ^ (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorType.LeftShift:
                return new BoundConstant((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorType.RightShift:
                return new BoundConstant((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorType.UnsignedRightShift:
                return new BoundConstant((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorType.Modulo:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue % (int)rightValue);
                else
                    return new BoundConstant((double)leftValue % (double)rightValue);
            default:
                throw new Exception($"Fold: unexpected binary operator '{op.opType}'");
        }
    }

    /// <summary>
    /// Folds a <see cref="UnaryExpression" /> (if possible).
    /// </summary>
    /// <param name="op">Operator.</param>
    /// <param name="operand">Operand.</param>
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant Fold(BoundUnaryOperator op, BoundExpression operand) {
        var operandType = operand.typeClause.lType;

        if (operand.constantValue != null && operand.constantValue.value is int value) {
            switch (op.opType) {
                case BoundUnaryOperatorType.NumericalIdentity:
                    if (operandType == TypeSymbol.Int)
                        return new BoundConstant((int)operand.constantValue.value);
                    else
                        return new BoundConstant((double)operand.constantValue.value);
                case BoundUnaryOperatorType.NumericalNegation:
                    if (operandType == TypeSymbol.Int)
                        return new BoundConstant(-(int)operand.constantValue.value);
                    else
                        return new BoundConstant(-(double)operand.constantValue.value);
                case BoundUnaryOperatorType.BooleanNegation:
                    return new BoundConstant(!(bool)operand.constantValue.value);
                case BoundUnaryOperatorType.BitwiseCompliment:
                    return new BoundConstant(~(int)operand.constantValue.value);
                default:
                    throw new Exception($"Fold: unexpected unary operator '{op.opType}'");
            }
        }

        return null;
    }

    internal static BoundConstant Fold(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        // TODO
        return null;
    }
}
