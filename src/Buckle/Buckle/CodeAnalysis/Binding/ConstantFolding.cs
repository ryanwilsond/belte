using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
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
        if (op?.opType == BoundBinaryOperatorKind.ConditionalAnd) {
            if ((leftConstant != null && leftConstant.value != null && !(bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && !(bool)rightConstant.value))
                return new BoundConstant(false);
        }

        if (op?.opType == BoundBinaryOperatorKind.ConditionalOr) {
            if ((leftConstant != null && leftConstant.value != null && (bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && (bool)rightConstant.value))
                return new BoundConstant(true);
        }

        if (op?.opType == BoundBinaryOperatorKind.NullCoalescing) {
            if (leftConstant != null && leftConstant.value != null)
                return new BoundConstant(leftConstant.value);

            if (leftConstant != null && leftConstant.value == null && rightConstant != null)
                return new BoundConstant(rightConstant.value);
        }

        if (leftConstant == null || rightConstant == null || op == null)
            return null;

        var leftValue = leftConstant.value;
        var rightValue = rightConstant.value;
        var leftType = op.leftType.typeSymbol;
        var rightType = op.rightType.typeSymbol;

        if (leftValue == null || rightValue == null)
            return new BoundConstant(null);

        leftValue = CastUtilities.Cast(leftValue, leftType);
        rightValue = CastUtilities.Cast(rightValue, leftType);

        switch (op.opType) {
            case BoundBinaryOperatorKind.Addition:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue + (int)rightValue);
                else if (leftType == TypeSymbol.String)
                    return new BoundConstant((string)leftValue + (string)rightValue);
                else
                    return new BoundConstant((double)leftValue + (double)rightValue);
            case BoundBinaryOperatorKind.Subtraction:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue - (int)rightValue);
                else
                    return new BoundConstant((double)leftValue - (double)rightValue);
            case BoundBinaryOperatorKind.Multiplication:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue * (int)rightValue);
                else
                    return new BoundConstant((double)leftValue * (double)rightValue);
            case BoundBinaryOperatorKind.Division:
                if (leftType == TypeSymbol.Int) {
                    if ((int)rightValue != 0)
                        return new BoundConstant((int)leftValue / (int)rightValue);
                } else {
                    if ((double)rightValue != 0)
                        return new BoundConstant((double)leftValue / (double)rightValue);
                }

                return null;
            case BoundBinaryOperatorKind.Power:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new BoundConstant((double)Math.Pow((double)leftValue, (double)rightValue));
            case BoundBinaryOperatorKind.ConditionalAnd:
                return new BoundConstant((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorKind.ConditionalOr:
                return new BoundConstant((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorKind.EqualityEquals:
                return new BoundConstant(Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.EqualityNotEquals:
                return new BoundConstant(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue < (int)rightValue);
                else
                    return new BoundConstant((double)leftValue < (double)rightValue);
            case BoundBinaryOperatorKind.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue > (int)rightValue);
                else
                    return new BoundConstant((double)leftValue > (double)rightValue);
            case BoundBinaryOperatorKind.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue <= (int)rightValue);
                else
                    return new BoundConstant((double)leftValue <= (double)rightValue);
            case BoundBinaryOperatorKind.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue >= (int)rightValue);
                else
                    return new BoundConstant((double)leftValue >= (double)rightValue);
            case BoundBinaryOperatorKind.LogicalAnd:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue & (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalOr:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue | (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalXor:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue ^ (int)rightValue);
                else
                    return new BoundConstant((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorKind.LeftShift:
                return new BoundConstant((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorKind.RightShift:
                return new BoundConstant((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorKind.UnsignedRightShift:
                return new BoundConstant((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorKind.Modulo:
                if (leftType == TypeSymbol.Int)
                    return new BoundConstant((int)leftValue % (int)rightValue);
                else
                    return new BoundConstant((double)leftValue % (double)rightValue);
            default:
                throw new BelteInternalException($"Fold: unexpected binary operator '{op.opType}'");
        }
    }

    /// <summary>
    /// Folds a <see cref="UnaryExpression" /> (if possible).
    /// </summary>
    /// <param name="op">Operator.</param>
    /// <param name="operand">Operand.</param>
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant Fold(BoundUnaryOperator op, BoundExpression operand) {
        var operandType = operand.type.typeSymbol;

        if (operand.constantValue != null && operand.constantValue.value is int value) {
            switch (op.opType) {
                case BoundUnaryOperatorKind.NumericalIdentity:
                    if (operandType == TypeSymbol.Int)
                        return new BoundConstant((int)operand.constantValue.value);
                    else
                        return new BoundConstant((double)operand.constantValue.value);
                case BoundUnaryOperatorKind.NumericalNegation:
                    if (operandType == TypeSymbol.Int)
                        return new BoundConstant(-(int)operand.constantValue.value);
                    else
                        return new BoundConstant(-(double)operand.constantValue.value);
                case BoundUnaryOperatorKind.BooleanNegation:
                    return new BoundConstant(!(bool)operand.constantValue.value);
                case BoundUnaryOperatorKind.BitwiseCompliment:
                    return new BoundConstant(~(int)operand.constantValue.value);
                default:
                    throw new BelteInternalException($"Fold: unexpected unary operator '{op.opType}'");
            }
        }

        return null;
    }

    internal static BoundConstant Fold(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        if (op.opType == BoundTernaryOperatorKind.Conditional) {
            if (left.constantValue != null && (bool)left.constantValue.value && center.constantValue != null)
                return new BoundConstant(center.constantValue.value);

            if (left.constantValue != null && !(bool)left.constantValue.value && right.constantValue != null)
                return new BoundConstant(right.constantValue.value);
        }

        return null;
    }
}
