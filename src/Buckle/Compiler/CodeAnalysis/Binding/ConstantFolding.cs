using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Folds/evaluates simple BoundExpressions during compile time.
/// </summary>
internal static class ConstantFolding {
    internal static ConstantValue FoldBinary(
        BoundExpression left,
        BoundExpression right,
        BinaryOperatorKind opKind,
        TypeSymbol type) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        if (opKind == BinaryOperatorKind.Error)
            return null;

        opKind &= BinaryOperatorKind.OpMask;

        // With and/or operators allow one side to be null
        if (opKind == BinaryOperatorKind.ConditionalAnd) {
            if ((leftConstant is not null && leftConstant.value is not null && !(bool)leftConstant.value) ||
                (rightConstant is not null && rightConstant.value is not null && !(bool)rightConstant.value)) {
                return new ConstantValue(false, SpecialType.Bool);
            }
        }

        if (opKind == BinaryOperatorKind.ConditionalOr) {
            if ((leftConstant is not null && leftConstant.value is not null && (bool)leftConstant.value) ||
                (rightConstant is not null && rightConstant.value is not null && (bool)rightConstant.value)) {
                return new ConstantValue(true, SpecialType.Bool);
            }
        }

        if (ConstantValue.IsNull(leftConstant) || ConstantValue.IsNull(rightConstant))
            return new ConstantValue(null, type.specialType);

        if (leftConstant is null || rightConstant is null)
            return null;

        var leftValue = leftConstant.value;
        var rightValue = rightConstant.value;
        var specialType = type.StrippedType().specialType;

        if (opKind is BinaryOperatorKind.Equal)
            return new ConstantValue(Equals(leftValue, rightValue));

        if (opKind is BinaryOperatorKind.NotEqual)
            return new ConstantValue(!Equals(leftValue, rightValue));

        if (!LiteralUtilities.TryCast(leftValue, type, out leftValue) ||
            !LiteralUtilities.TryCast(rightValue, type, out rightValue)) {
            return null;
        }

        switch (opKind) {
            case BinaryOperatorKind.Addition:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue + (long)rightValue, specialType);
                else if (specialType == SpecialType.String)
                    return new ConstantValue((string)leftValue + (string)rightValue, specialType);
                else
                    return new ConstantValue((double)leftValue + (double)rightValue, specialType);
            case BinaryOperatorKind.Subtraction:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue - (long)rightValue, specialType);
                else
                    return new ConstantValue((double)leftValue - (double)rightValue, specialType);
            case BinaryOperatorKind.Multiplication:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue * (long)rightValue, specialType);
                else
                    return new ConstantValue((double)leftValue * (double)rightValue, specialType);
            case BinaryOperatorKind.Division:
                if (specialType == SpecialType.Int) {
                    if ((long)rightValue != 0)
                        return new ConstantValue((long)leftValue / (long)rightValue, specialType);
                } else {
                    if ((double)rightValue != 0)
                        return new ConstantValue((double)leftValue / (double)rightValue, specialType);
                }

                return null;
            case BinaryOperatorKind.Power:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)Math.Pow((long)leftValue, (long)rightValue), specialType);
                else
                    return new ConstantValue(Math.Pow((double)leftValue, (double)rightValue), specialType);
            case BinaryOperatorKind.ConditionalAnd:
                return new ConstantValue((bool)leftValue && (bool)rightValue, specialType);
            case BinaryOperatorKind.ConditionalOr:
                return new ConstantValue((bool)leftValue || (bool)rightValue, specialType);
            case BinaryOperatorKind.Equal:
                return new ConstantValue(Equals(leftValue, rightValue), specialType);
            case BinaryOperatorKind.NotEqual:
                return new ConstantValue(!Equals(leftValue, rightValue), specialType);
            case BinaryOperatorKind.LessThan:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue < (long)rightValue, SpecialType.Bool);
                else
                    return new ConstantValue((double)leftValue < (double)rightValue, SpecialType.Bool);
            case BinaryOperatorKind.GreaterThan:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue > (long)rightValue, SpecialType.Bool);
                else
                    return new ConstantValue((double)leftValue > (double)rightValue, SpecialType.Bool);
            case BinaryOperatorKind.LessThanOrEqual:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue <= (long)rightValue, SpecialType.Bool);
                else
                    return new ConstantValue((double)leftValue <= (double)rightValue, SpecialType.Bool);
            case BinaryOperatorKind.GreaterThanOrEqual:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue >= (long)rightValue, SpecialType.Bool);
                else
                    return new ConstantValue((double)leftValue >= (double)rightValue, SpecialType.Bool);
            case BinaryOperatorKind.And:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue & (long)rightValue, specialType);
                else
                    return new ConstantValue((bool)leftValue & (bool)rightValue, specialType);
            case BinaryOperatorKind.Or:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue | (long)rightValue, specialType);
                else
                    return new ConstantValue((bool)leftValue | (bool)rightValue, specialType);
            case BinaryOperatorKind.Xor:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue ^ (long)rightValue, specialType);
                else
                    return new ConstantValue((bool)leftValue ^ (bool)rightValue, specialType);
            case BinaryOperatorKind.LeftShift:
                return new ConstantValue((long)leftValue << Convert.ToInt32(rightValue), specialType);
            case BinaryOperatorKind.RightShift:
                return new ConstantValue((long)leftValue >> Convert.ToInt32(rightValue), specialType);
            case BinaryOperatorKind.UnsignedRightShift:
                return new ConstantValue((long)leftValue >>> Convert.ToInt32(rightValue), specialType);
            case BinaryOperatorKind.Modulo:
                if (specialType == SpecialType.Int)
                    return new ConstantValue((long)leftValue % (long)rightValue, specialType);
                else
                    return new ConstantValue((double)leftValue % (double)rightValue, specialType);
            default:
                throw ExceptionUtilities.UnexpectedValue(opKind);
        }
    }

    internal static ConstantValue FoldNullCoalescing(BoundExpression left, BoundExpression right, TypeSymbol type) {
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;
        var specialType = type.specialType;

        if (leftConstant is not null && leftConstant.value is not null)
            return new ConstantValue(leftConstant.value, specialType);

        if (leftConstant is not null && leftConstant.value is null && rightConstant is not null)
            return new ConstantValue(rightConstant.value, specialType);

        return null;
    }

    internal static ConstantValue FoldIs(BoundExpression left, BoundExpression right, bool isNot) {
        // TODO Should be able to expand this to cover some `is object` or `is primitive` expressions too
        var leftConstant = left.constantValue;
        var rightConstant = right.constantValue;

        if (ConstantValue.IsNull(leftConstant) && ConstantValue.IsNull(rightConstant))
            return new ConstantValue(!isNot, SpecialType.Bool);

        if (ConstantValue.IsNotNull(leftConstant) && ConstantValue.IsNull(rightConstant))
            return new ConstantValue(isNot, SpecialType.Bool);

        return null;
    }

    internal static ConstantValue FoldNullAssert(BoundExpression operand) {
        if (ConstantValue.IsNotNull(operand.constantValue))
            return operand.constantValue;

        return null;
    }

    internal static ConstantValue FoldUnary(BoundExpression operand, UnaryOperatorKind opKind, TypeSymbol type) {
        if (opKind == UnaryOperatorKind.Error)
            return null;

        opKind &= UnaryOperatorKind.OpMask;

        var operandSpecialType = operand.type.specialType;

        if (operand.constantValue is null || opKind == UnaryOperatorKind.Error)
            return null;

        var value = operand.constantValue.value;
        var specialType = type.specialType;

        if (value is null)
            return new ConstantValue(null, specialType);

        switch (opKind) {
            case UnaryOperatorKind.UnaryPlus:
                return operand.constantValue;
            case UnaryOperatorKind.UnaryMinus:
                if (operandSpecialType == SpecialType.Int)
                    return new ConstantValue(-(long)operand.constantValue.value, specialType);
                else
                    return new ConstantValue(-(double)operand.constantValue.value, specialType);
            case UnaryOperatorKind.LogicalNegation:
                return new ConstantValue(!(bool)operand.constantValue.value, specialType);
            case UnaryOperatorKind.BitwiseComplement:
                return new ConstantValue(~(long)operand.constantValue.value, specialType);
            default:
                throw ExceptionUtilities.UnexpectedValue(opKind);
        }
    }

    /// <summary>
    /// Folds a <see cref="BoundConditionalOperatorExpression" /> (if possible).
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="center">Center operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldConditional(
        BoundExpression left,
        BoundExpression center,
        BoundExpression right,
        TypeSymbol type) {
        var specialType = type.specialType;

        if (ConstantValue.IsNotNull(left.constantValue) &&
            (bool)left.constantValue.value &&
            center.constantValue is not null) {
            return new ConstantValue(center.constantValue.value, specialType);
        }

        if (ConstantValue.IsNotNull(left.constantValue) &&
            !(bool)left.constantValue.value &&
            right.constantValue is not null) {
            return new ConstantValue(right.constantValue.value, specialType);
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundCastExpression" /> (if possible).
    /// </summary>
    /// <param name="expression">Expression operand.</param>
    /// <param name="type">Casting to type.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldCast(BoundExpression expression, TypeWithAnnotations type) {
        if (expression.constantValue is null)
            return null;

        if (expression.constantValue.value is null && !type.isNullable)
            return null;

        var specialType = type.type.StrippedType().specialType;

        // Preserve "actual" type
        if (specialType == SpecialType.Any)
            return expression.constantValue;

        if (LiteralUtilities.TryCast(expression.constantValue.value, type, out var castedValue))
            return new ConstantValue(castedValue, specialType);

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundInitializerListExpression" /> (if possible).
    /// </summary>
    /// <param name="items">Initializer list contents.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldInitializerList(ImmutableArray<BoundExpression> items) {
        var foldedItems = ArrayBuilder<ConstantValue>.GetInstance();

        foreach (var item in items) {
            if (item.constantValue is not null)
                foldedItems.Add(item.constantValue);
            else
                return null;
        }

        return new ConstantValue(foldedItems.ToImmutableAndFree(), SpecialType.None);
    }

    /// <summary>
    /// Folds a <see cref="BoundArrayAccessExpression"/> (if possible).
    /// </summary>
    /// <param name="expression">The expression being indexed.</param>
    /// <param name="index">The index.</param>
    /// <returns>The constant item at the index, if constant.</returns>
    internal static ConstantValue FoldIndex(BoundExpression expression, BoundExpression index, TypeSymbol type) {
        if (expression.constantValue is null || index.constantValue is null)
            return null;

        var array = (ImmutableArray<ConstantValue>)expression.constantValue.value;
        var item = array[Convert.ToInt32(index.constantValue.value)];
        var specialType = type.specialType;

        return new ConstantValue(item.value, specialType);
    }
}
