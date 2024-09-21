using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Folds/evaluates simple BoundExpressions during compile time.
/// </summary>
internal static class ConstantFolding {
    /// <summary>
    /// Folds a <see cref="BoundBinaryExpression" /> (if possible).
    /// </summary>
    /// <param name="left">Left side operand.</param>
    /// <param name="op">Operator.</param>
    /// <param name="right">Right side operand.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldBinary(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        if (op is null)
            return null;

        // With and/or operators allow one side to be null
        if (op.opKind == BoundBinaryOperatorKind.ConditionalAnd) {
            if ((leftConstant != null && leftConstant.value != null && !(bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && !(bool)rightConstant.value)) {
                return new ConstantValue(false);
            }
        }

        if (op.opKind == BoundBinaryOperatorKind.ConditionalOr) {
            if ((leftConstant != null && leftConstant.value != null && (bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && (bool)rightConstant.value)) {
                return new ConstantValue(true);
            }
        }

        if (op.opKind == BoundBinaryOperatorKind.NullCoalescing) {
            if (leftConstant != null && leftConstant.value != null)
                return new ConstantValue(leftConstant.value);

            if (leftConstant != null && leftConstant.value is null && rightConstant != null)
                return new ConstantValue(rightConstant.value);
        }

        if (op.opKind == BoundBinaryOperatorKind.Is) {
            if (ConstantValue.IsNull(leftConstant) && ConstantValue.IsNull(rightConstant))
                return new ConstantValue(true);

            if (ConstantValue.IsNotNull(leftConstant) && ConstantValue.IsNull(rightConstant))
                return new ConstantValue(false);
        }

        if (op.opKind == BoundBinaryOperatorKind.Isnt) {
            if (ConstantValue.IsNull(leftConstant) && ConstantValue.IsNull(rightConstant))
                return new ConstantValue(false);

            if (ConstantValue.IsNotNull(leftConstant) && ConstantValue.IsNull(rightConstant))
                return new ConstantValue(true);
        }

        if ((ConstantValue.IsNull(leftConstant) || ConstantValue.IsNull(rightConstant)) &&
            op.opKind != BoundBinaryOperatorKind.Is && op.opKind != BoundBinaryOperatorKind.Isnt) {
            return new ConstantValue(null);
        }

        if (leftConstant is null || rightConstant is null)
            return null;

        var leftValue = leftConstant.value;
        var rightValue = rightConstant.value;
        var leftType = op.leftType.typeSymbol;

        leftValue = CastUtilities.Cast(leftValue, op.leftType);
        rightValue = CastUtilities.Cast(rightValue, op.rightType);

        switch (op.opKind) {
            case BoundBinaryOperatorKind.Addition:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue + (int)rightValue);
                else if (leftType == TypeSymbol.String)
                    return new ConstantValue((string)leftValue + (string)rightValue);
                else
                    return new ConstantValue((double)leftValue + (double)rightValue);
            case BoundBinaryOperatorKind.Subtraction:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue - (int)rightValue);
                else
                    return new ConstantValue((double)leftValue - (double)rightValue);
            case BoundBinaryOperatorKind.Multiplication:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue * (int)rightValue);
                else
                    return new ConstantValue((double)leftValue * (double)rightValue);
            case BoundBinaryOperatorKind.Division:
                if (leftType == TypeSymbol.Int) {
                    if ((int)rightValue != 0)
                        return new ConstantValue((int)leftValue / (int)rightValue);
                } else {
                    if ((double)rightValue != 0)
                        return new ConstantValue((double)leftValue / (double)rightValue);
                }

                return null;
            case BoundBinaryOperatorKind.Power:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new ConstantValue((double)Math.Pow((double)leftValue, (double)rightValue));
            case BoundBinaryOperatorKind.ConditionalAnd:
                return new ConstantValue((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorKind.ConditionalOr:
                return new ConstantValue((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorKind.EqualityEquals:
                return new ConstantValue(Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.EqualityNotEquals:
                return new ConstantValue(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue < (int)rightValue);
                else
                    return new ConstantValue((double)leftValue < (double)rightValue);
            case BoundBinaryOperatorKind.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue > (int)rightValue);
                else
                    return new ConstantValue((double)leftValue > (double)rightValue);
            case BoundBinaryOperatorKind.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue <= (int)rightValue);
                else
                    return new ConstantValue((double)leftValue <= (double)rightValue);
            case BoundBinaryOperatorKind.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue >= (int)rightValue);
                else
                    return new ConstantValue((double)leftValue >= (double)rightValue);
            case BoundBinaryOperatorKind.LogicalAnd:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue & (int)rightValue);
                else
                    return new ConstantValue((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalOr:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue | (int)rightValue);
                else
                    return new ConstantValue((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalXor:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue ^ (int)rightValue);
                else
                    return new ConstantValue((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorKind.LeftShift:
                return new ConstantValue((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorKind.RightShift:
                return new ConstantValue((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorKind.UnsignedRightShift:
                return new ConstantValue((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorKind.Modulo:
                if (leftType == TypeSymbol.Int)
                    return new ConstantValue((int)leftValue % (int)rightValue);
                else
                    return new ConstantValue((double)leftValue % (double)rightValue);
            default:
                throw new BelteInternalException($"Fold: unexpected binary operator '{op.opKind}'");
        }
    }

    /// <summary>
    /// Folds a <see cref="BoundUnaryExpression" /> (if possible).
    /// </summary>
    /// <param name="op">Operator.</param>
    /// <param name="operand">Operand.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldUnary(BoundUnaryOperator op, BoundExpression operand) {
        var operandType = operand.type.typeSymbol;

        if (operand.constantValue is null || op is null)
            return null;

        var value = operand.constantValue.value;

        if (value is null)
            return new ConstantValue(null);

        switch (op.opKind) {
            case BoundUnaryOperatorKind.NumericalIdentity:
                if (operandType == TypeSymbol.Int)
                    return new ConstantValue((int)operand.constantValue.value);
                else
                    return new ConstantValue((double)operand.constantValue.value);
            case BoundUnaryOperatorKind.NumericalNegation:
                if (operandType == TypeSymbol.Int)
                    return new ConstantValue(-(int)operand.constantValue.value);
                else
                    return new ConstantValue(-(double)operand.constantValue.value);
            case BoundUnaryOperatorKind.BooleanNegation:
                return new ConstantValue(!(bool)operand.constantValue.value);
            case BoundUnaryOperatorKind.BitwiseCompliment:
                return new ConstantValue(~(int)operand.constantValue.value);
            default:
                throw new BelteInternalException($"Fold: unexpected unary operator '{op.opKind}'");
        }
    }

    /// <summary>
    /// Folds a <see cref="BoundTernaryExpression" /> (if possible).
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="op">Operator.</param>
    /// <param name="center">Center operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldTernary(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        if (op.opKind == BoundTernaryOperatorKind.Conditional) {
            if (ConstantValue.IsNotNull(left.constantValue) &&
                (bool)left.constantValue.value &&
                center.constantValue != null) {
                return new ConstantValue(center.constantValue.value);
            }

            if (ConstantValue.IsNotNull(left.constantValue) &&
                !(bool)left.constantValue.value &&
                right.constantValue != null) {
                return new ConstantValue(right.constantValue.value);
            }
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundCastExpression" /> (if possible).
    /// </summary>
    /// <param name="expression">Expression operand.</param>
    /// <param name="type">Casting to type.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldCast(BoundExpression expression, BoundType type) {
        if (expression.constantValue != null) {
            if (expression.constantValue.value is null && !type.isNullable)
                return null;

            try {
                return new ConstantValue(CastUtilities.Cast(expression.constantValue.value, type));
            } catch (Exception e) when (e is FormatException || e is InvalidCastException) {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundInitializerListExpression" /> (if possible).
    /// </summary>
    /// <param name="items">Initializer list contents.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldInitializerList(ImmutableArray<BoundExpression> items) {
        var foldedItems = ImmutableArray.CreateBuilder<ConstantValue>();

        foreach (var item in items) {
            if (item.constantValue != null)
                foldedItems.Add(item.constantValue);
            else
                return null;
        }

        return new ConstantValue(foldedItems.ToImmutable());
    }

    /// <summary>
    /// Folds a <see cref="BoundIndexExpression"/> (if possible).
    /// </summary>
    /// <param name="expression">The expression being indexed.</param>
    /// <param name="index">The index.</param>
    /// <returns>The constant item at the index, if constant.</returns>
    internal static ConstantValue FoldIndex(BoundExpression expression, BoundExpression index) {
        if (expression.constantValue is null || index.constantValue is null)
            return null;

        var array = (ImmutableArray<ConstantValue>)expression.constantValue.value;
        var item = array[(int)index.constantValue.value];

        return new ConstantValue(item.value);
    }
}
