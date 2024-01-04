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
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant FoldBinary(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        if (op is null)
            return null;

        // With and/or operators allow one side to be null
        if (op.opKind == BoundBinaryOperatorKind.ConditionalAnd) {
            if ((leftConstant != null && leftConstant.value != null && !(bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && !(bool)rightConstant.value)) {
                return new BoundConstant(false);
            }
        }

        if (op.opKind == BoundBinaryOperatorKind.ConditionalOr) {
            if ((leftConstant != null && leftConstant.value != null && (bool)leftConstant.value) ||
                (rightConstant != null && rightConstant.value != null && (bool)rightConstant.value)) {
                return new BoundConstant(true);
            }
        }

        if (op.opKind == BoundBinaryOperatorKind.NullCoalescing) {
            if (leftConstant != null && leftConstant.value != null)
                return new BoundConstant(leftConstant.value);

            if (leftConstant != null && leftConstant.value is null && rightConstant != null)
                return new BoundConstant(rightConstant.value);
        }

        if (op.opKind == BoundBinaryOperatorKind.Is) {
            if (BoundConstant.IsNull(leftConstant) && BoundConstant.IsNull(rightConstant))
                return new BoundConstant(true);

            if (BoundConstant.IsNotNull(leftConstant) && BoundConstant.IsNull(rightConstant))
                return new BoundConstant(false);
        }

        if (op.opKind == BoundBinaryOperatorKind.Isnt) {
            if (BoundConstant.IsNull(leftConstant) && BoundConstant.IsNull(rightConstant))
                return new BoundConstant(false);

            if (BoundConstant.IsNotNull(leftConstant) && BoundConstant.IsNull(rightConstant))
                return new BoundConstant(true);
        }

        if ((BoundConstant.IsNull(leftConstant) || BoundConstant.IsNull(rightConstant)) &&
            op.opKind != BoundBinaryOperatorKind.Is && op.opKind != BoundBinaryOperatorKind.Isnt) {
            return new BoundConstant(null);
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
                throw new BelteInternalException($"Fold: unexpected binary operator '{op.opKind}'");
        }
    }

    /// <summary>
    /// Folds a <see cref="BoundUnaryExpression" /> (if possible).
    /// </summary>
    /// <param name="op">Operator.</param>
    /// <param name="operand">Operand.</param>
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant FoldUnary(BoundUnaryOperator op, BoundExpression operand) {
        var operandType = operand.type.typeSymbol;

        if (operand.constantValue is null || op is null)
            return null;

        var value = operand.constantValue.value;

        if (value is null)
            return new BoundConstant(null);

        switch (op.opKind) {
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
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant FoldTernary(
        BoundExpression left, BoundTernaryOperator op, BoundExpression center, BoundExpression right) {
        if (op.opKind == BoundTernaryOperatorKind.Conditional) {
            if (BoundConstant.IsNotNull(left.constantValue) &&
                (bool)left.constantValue.value &&
                center.constantValue != null) {
                return new BoundConstant(center.constantValue.value);
            }

            if (BoundConstant.IsNotNull(left.constantValue) &&
                !(bool)left.constantValue.value &&
                right.constantValue != null) {
                return new BoundConstant(right.constantValue.value);
            }
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundCastExpression" /> (if possible).
    /// </summary>
    /// <param name="expression">Expression operand.</param>
    /// <param name="type">Casting to type.</param>
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant FoldCast(BoundExpression expression, BoundType type) {
        if (expression.constantValue != null) {
            if (expression.constantValue.value is null && !type.isNullable)
                return null;

            try {
                return new BoundConstant(CastUtilities.Cast(expression.constantValue.value, type));
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
    /// <returns><see cref="BoundConstant" />, returns null if folding is not possible.</returns>
    internal static BoundConstant FoldInitializerList(ImmutableArray<BoundExpression> items) {
        var foldedItems = ImmutableArray.CreateBuilder<BoundConstant>();

        foreach (var item in items) {
            if (item.constantValue != null)
                foldedItems.Add(item.constantValue);
            else
                return null;
        }

        return new BoundConstant(foldedItems.ToImmutable());
    }
}
