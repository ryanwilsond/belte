using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
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
        TypeSymbol type,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics) {
        return FoldBinary(
            left.constantValue,
            left.type,
            right.constantValue,
            right.type,
            opKind,
            type,
            errorLocation,
            diagnostics
        );
    }

    internal static ConstantValue FoldBinary(
        ConstantValue left,
        TypeSymbol leftType,
        ConstantValue right,
        TypeSymbol rightType,
        BinaryOperatorKind opKind,
        TypeSymbol type,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics) {
        if (opKind == BinaryOperatorKind.Error)
            return null;

        opKind &= BinaryOperatorKind.OpMask;

        // With and/or operators allow one side to be null
        if (opKind == BinaryOperatorKind.ConditionalAnd) {
            if ((left is not null && left.value is not null && !(bool)left.value) ||
                (right is not null && right.value is not null && !(bool)right.value)) {
                return new ConstantValue(false, SpecialType.Bool);
            }
        }

        if (opKind == BinaryOperatorKind.ConditionalOr) {
            if ((left is not null && left.value is not null && (bool)left.value) ||
                (right is not null && right.value is not null && (bool)right.value)) {
                return new ConstantValue(true, SpecialType.Bool);
            }
        }

        if (ConstantValue.IsNull(left) || ConstantValue.IsNull(right))
            return new ConstantValue(null, type.specialType);

        if (left is null || right is null)
            return null;

        if (leftType.IsEnumType())
            leftType = ((NamedTypeSymbol)leftType).enumUnderlyingType;

        if (rightType.IsEnumType())
            rightType = ((NamedTypeSymbol)rightType).enumUnderlyingType;

        if (type.IsEnumType())
            type = ((NamedTypeSymbol)type).enumUnderlyingType;

        var leftValue = left.value;
        var rightValue = right.value;
        var specialType = type.StrippedType().specialType;
        var normalizedType = CodeGenerator.NormalizeNumericType(specialType);

        if (opKind is BinaryOperatorKind.Equal)
            return new ConstantValue(Equals(leftValue, rightValue), SpecialType.Bool);

        if (opKind is BinaryOperatorKind.NotEqual)
            return new ConstantValue(!Equals(leftValue, rightValue), SpecialType.Bool);

        if (!LiteralUtilities.TryCast(leftValue, leftType, type, errorLocation, diagnostics, out leftValue) ||
            !LiteralUtilities.TryCast(rightValue, rightType, type, errorLocation, diagnostics, out rightValue)) {
            return null;
        }

        switch (opKind) {
            case BinaryOperatorKind.Addition:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue + (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue + (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue + (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue + (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue + (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue + (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue + (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue + (ulong)rightValue, specialType),
                    SpecialType.Float32 => new ConstantValue((float)leftValue + (float)rightValue, specialType),
                    SpecialType.Float64 => new ConstantValue((double)leftValue + (double)rightValue, specialType),
                    SpecialType.String => new ConstantValue((string)leftValue + (string)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Subtraction:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue - (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue - (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue - (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue - (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue - (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue - (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue - (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue - (ulong)rightValue, specialType),
                    SpecialType.Float32 => new ConstantValue((float)leftValue - (float)rightValue, specialType),
                    SpecialType.Float64 => new ConstantValue((double)leftValue - (double)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Multiplication:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue * (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue * (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue * (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue * (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue * (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue * (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue * (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue * (ulong)rightValue, specialType),
                    SpecialType.Float32 => new ConstantValue((float)leftValue * (float)rightValue, specialType),
                    SpecialType.Float64 => new ConstantValue((double)leftValue * (double)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Division:
                if (Convert.ToByte(rightValue) == 0)
                    return null;

                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue / (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue / (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue / (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue / (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue / (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue / (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue / (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue / (ulong)rightValue, specialType),
                    SpecialType.Float32 => new ConstantValue((float)leftValue / (float)rightValue, specialType),
                    SpecialType.Float64 => new ConstantValue((double)leftValue / (double)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Power:
                // TODO We should reconsider if we want to always expand to int64 here
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue(Convert.ToInt64(Math.Pow((sbyte)leftValue, (sbyte)rightValue)), specialType),
                    SpecialType.Int16 => new ConstantValue(Convert.ToInt64(Math.Pow((short)leftValue, (short)rightValue)), specialType),
                    SpecialType.Int32 => new ConstantValue(Convert.ToInt64(Math.Pow((int)leftValue, (int)rightValue)), specialType),
                    SpecialType.Int64 => new ConstantValue(Convert.ToInt64(Math.Pow((long)leftValue, (long)rightValue)), specialType),
                    SpecialType.UInt8 => new ConstantValue(Convert.ToInt64(Math.Pow((byte)leftValue, (byte)rightValue)), specialType),
                    SpecialType.UInt16 => new ConstantValue(Convert.ToInt64(Math.Pow((ushort)leftValue, (ushort)rightValue)), specialType),
                    SpecialType.UInt32 => new ConstantValue(Convert.ToInt64(Math.Pow((uint)leftValue, (uint)rightValue)), specialType),
                    SpecialType.UInt64 => new ConstantValue(Convert.ToInt64(Math.Pow((ulong)leftValue, (ulong)rightValue)), specialType),
                    SpecialType.Float32 => new ConstantValue(Math.Pow((float)leftValue, (float)rightValue), specialType),
                    SpecialType.Float64 => new ConstantValue(Math.Pow((double)leftValue, (double)rightValue), specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.ConditionalAnd:
                return new ConstantValue((bool)leftValue && (bool)rightValue, specialType);
            case BinaryOperatorKind.ConditionalOr:
                return new ConstantValue((bool)leftValue || (bool)rightValue, specialType);
            case BinaryOperatorKind.Equal:
                return new ConstantValue(Equals(leftValue, rightValue), SpecialType.Bool);
            case BinaryOperatorKind.NotEqual:
                return new ConstantValue(!Equals(leftValue, rightValue), SpecialType.Bool);
            case BinaryOperatorKind.LessThan:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue < (sbyte)rightValue, SpecialType.Bool),
                    SpecialType.Int16 => new ConstantValue((short)leftValue < (short)rightValue, SpecialType.Bool),
                    SpecialType.Int32 => new ConstantValue((int)leftValue < (int)rightValue, SpecialType.Bool),
                    SpecialType.Int64 => new ConstantValue((long)leftValue < (long)rightValue, SpecialType.Bool),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue < (byte)rightValue, SpecialType.Bool),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue < (ushort)rightValue, SpecialType.Bool),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue < (uint)rightValue, SpecialType.Bool),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue < (ulong)rightValue, SpecialType.Bool),
                    SpecialType.Float32 => new ConstantValue((float)leftValue < (float)rightValue, SpecialType.Bool),
                    SpecialType.Float64 => new ConstantValue((double)leftValue < (double)rightValue, SpecialType.Bool),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.GreaterThan:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue > (sbyte)rightValue, SpecialType.Bool),
                    SpecialType.Int16 => new ConstantValue((short)leftValue > (short)rightValue, SpecialType.Bool),
                    SpecialType.Int32 => new ConstantValue((int)leftValue > (int)rightValue, SpecialType.Bool),
                    SpecialType.Int64 => new ConstantValue((long)leftValue > (long)rightValue, SpecialType.Bool),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue > (byte)rightValue, SpecialType.Bool),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue > (ushort)rightValue, SpecialType.Bool),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue > (uint)rightValue, SpecialType.Bool),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue > (ulong)rightValue, SpecialType.Bool),
                    SpecialType.Float32 => new ConstantValue((float)leftValue > (float)rightValue, SpecialType.Bool),
                    SpecialType.Float64 => new ConstantValue((double)leftValue > (double)rightValue, SpecialType.Bool),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.LessThanOrEqual:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue <= (sbyte)rightValue, SpecialType.Bool),
                    SpecialType.Int16 => new ConstantValue((short)leftValue <= (short)rightValue, SpecialType.Bool),
                    SpecialType.Int32 => new ConstantValue((int)leftValue <= (int)rightValue, SpecialType.Bool),
                    SpecialType.Int64 => new ConstantValue((long)leftValue <= (long)rightValue, SpecialType.Bool),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue <= (byte)rightValue, SpecialType.Bool),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue <= (ushort)rightValue, SpecialType.Bool),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue <= (uint)rightValue, SpecialType.Bool),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue <= (ulong)rightValue, SpecialType.Bool),
                    SpecialType.Float32 => new ConstantValue((float)leftValue <= (float)rightValue, SpecialType.Bool),
                    SpecialType.Float64 => new ConstantValue((double)leftValue <= (double)rightValue, SpecialType.Bool),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.GreaterThanOrEqual:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue >= (sbyte)rightValue, SpecialType.Bool),
                    SpecialType.Int16 => new ConstantValue((short)leftValue >= (short)rightValue, SpecialType.Bool),
                    SpecialType.Int32 => new ConstantValue((int)leftValue >= (int)rightValue, SpecialType.Bool),
                    SpecialType.Int64 => new ConstantValue((long)leftValue >= (long)rightValue, SpecialType.Bool),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue >= (byte)rightValue, SpecialType.Bool),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue >= (ushort)rightValue, SpecialType.Bool),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue >= (uint)rightValue, SpecialType.Bool),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue >= (ulong)rightValue, SpecialType.Bool),
                    SpecialType.Float32 => new ConstantValue((float)leftValue >= (float)rightValue, SpecialType.Bool),
                    SpecialType.Float64 => new ConstantValue((double)leftValue >= (double)rightValue, SpecialType.Bool),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.And:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue & (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue & (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue & (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue & (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue & (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue & (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue & (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue & (ulong)rightValue, specialType),
                    SpecialType.Bool => new ConstantValue((bool)leftValue & (bool)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Or:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue | (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue | (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue | (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue | (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue | (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue | (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue | (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue | (ulong)rightValue, specialType),
                    SpecialType.Bool => new ConstantValue((bool)leftValue | (bool)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Xor:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue ^ (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue ^ (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue ^ (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue ^ (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue ^ (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue ^ (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue ^ (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue ^ (ulong)rightValue, specialType),
                    SpecialType.Bool => new ConstantValue((bool)leftValue ^ (bool)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.LeftShift:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue << (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue << (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue << (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue << Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue << (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue << (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue << Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue << Convert.ToInt32(rightValue), specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.RightShift:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue >> (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue >> (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue >> (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue >> Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue >> (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue >> (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue >> Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue >> Convert.ToInt32(rightValue), specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.UnsignedRightShift:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue >>> (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue >>> (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue >>> (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue >>> Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue >>> (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue >>> (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue >>> Convert.ToInt32(rightValue), specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue >>> Convert.ToInt32(rightValue), specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case BinaryOperatorKind.Modulo:
                return normalizedType switch {
                    SpecialType.Int8 => new ConstantValue((sbyte)leftValue % (sbyte)rightValue, specialType),
                    SpecialType.Int16 => new ConstantValue((short)leftValue % (short)rightValue, specialType),
                    SpecialType.Int32 => new ConstantValue((int)leftValue % (int)rightValue, specialType),
                    SpecialType.Int64 => new ConstantValue((long)leftValue % (long)rightValue, specialType),
                    SpecialType.UInt8 => new ConstantValue((byte)leftValue % (byte)rightValue, specialType),
                    SpecialType.UInt16 => new ConstantValue((ushort)leftValue % (ushort)rightValue, specialType),
                    SpecialType.UInt32 => new ConstantValue((uint)leftValue % (uint)rightValue, specialType),
                    SpecialType.UInt64 => new ConstantValue((ulong)leftValue % (ulong)rightValue, specialType),
                    SpecialType.Float32 => new ConstantValue((float)leftValue % (float)rightValue, specialType),
                    SpecialType.Float64 => new ConstantValue((double)leftValue % (double)rightValue, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            default:
                throw ExceptionUtilities.UnexpectedValue(opKind);
        }
    }

    internal static ConstantValue FoldNullCoalescing(
        BoundExpression left,
        BoundExpression right,
        bool isPropagation,
        TypeSymbol type) {
        return FoldNullCoalescing(left.constantValue, right.constantValue, isPropagation, type);
    }

    internal static ConstantValue FoldNullCoalescing(
        ConstantValue left,
        ConstantValue right,
        bool isPropagation,
        TypeSymbol type) {
        var specialType = type.specialType;

        if (isPropagation) {
            if (left is not null && left.value is null)
                return new ConstantValue(left.value, specialType);

            if (left is not null && left.value is not null && right is not null)
                return new ConstantValue(right.value, specialType);
        } else {
            if (left is not null && left.value is not null)
                return new ConstantValue(left.value, specialType);

            if (left is not null && left.value is null && right is not null)
                return new ConstantValue(right.value, specialType);
        }

        return null;
    }

    internal static ConstantValue FoldIs(BoundExpression left, BoundExpression right, bool isNot) {
        return FoldIs(left.constantValue, right.constantValue, isNot);
    }

    internal static ConstantValue FoldIs(ConstantValue left, ConstantValue right, bool isNot) {
        // TODO Should be able to expand this to cover some `is object` or `is primitive` expressions too

        if (ConstantValue.IsNull(left) && ConstantValue.IsNull(right))
            return new ConstantValue(!isNot, SpecialType.Bool);

        if (ConstantValue.IsNotNull(left) && ConstantValue.IsNull(right))
            return new ConstantValue(isNot, SpecialType.Bool);

        return null;
    }

    internal static ConstantValue FoldNullAssert(BoundExpression operand) {
        return FoldNullAssert(operand.constantValue);
    }

    internal static ConstantValue FoldNullAssert(ConstantValue operand) {
        if (ConstantValue.IsNotNull(operand))
            return operand;

        return null;
    }

    internal static ConstantValue FoldUnary(BoundExpression operand, UnaryOperatorKind opKind, TypeSymbol type) {
        return FoldUnary(operand.constantValue, opKind, type);
    }

    internal static ConstantValue FoldUnary(ConstantValue operand, UnaryOperatorKind opKind, TypeSymbol type) {
        if (opKind == UnaryOperatorKind.Error)
            return null;

        opKind &= UnaryOperatorKind.OpMask;

        var operandSpecialType = CodeGenerator.NormalizeNumericType(type.UnderlyingTemplateTypeOrSelf().specialType);

        if (operand is null || opKind == UnaryOperatorKind.Error)
            return null;

        var value = operand.value;
        var specialType = type.specialType;

        if (value is null)
            return new ConstantValue(null, specialType);

        switch (opKind) {
            case UnaryOperatorKind.UnaryPlus:
                return operand;
            case UnaryOperatorKind.UnaryMinus:
                return operandSpecialType switch {
                    SpecialType.Int8 => new ConstantValue(-(sbyte)value, specialType),
                    SpecialType.Int16 => new ConstantValue(-(short)value, specialType),
                    SpecialType.Int32 => new ConstantValue(-(int)value, specialType),
                    SpecialType.Int64 => new ConstantValue(-(long)value, specialType),
                    SpecialType.UInt8 => new ConstantValue(-(byte)value, specialType),
                    SpecialType.UInt16 => new ConstantValue(-(ushort)value, specialType),
                    SpecialType.UInt32 => new ConstantValue(-(uint)value, specialType),
                    SpecialType.UInt64 => new ConstantValue(-Convert.ToInt64(value), specialType),
                    SpecialType.Float32 => new ConstantValue(-(float)value, specialType),
                    SpecialType.Float64 => new ConstantValue(-(double)value, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
            case UnaryOperatorKind.LogicalNegation:
                return new ConstantValue(!(bool)operand.value, specialType);
            case UnaryOperatorKind.BitwiseComplement:
                return operandSpecialType switch {
                    SpecialType.Int8 => new ConstantValue(~(sbyte)value, specialType),
                    SpecialType.Int16 => new ConstantValue(~(short)value, specialType),
                    SpecialType.Int32 => new ConstantValue(~(int)value, specialType),
                    SpecialType.Int64 => new ConstantValue(~(long)value, specialType),
                    SpecialType.UInt8 => new ConstantValue(~(byte)value, specialType),
                    SpecialType.UInt16 => new ConstantValue(~(ushort)value, specialType),
                    SpecialType.UInt32 => new ConstantValue(~(uint)value, specialType),
                    SpecialType.UInt64 => new ConstantValue(~(long)value, specialType),
                    _ => throw ExceptionUtilities.UnexpectedValue(specialType),
                };
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
        return FoldConditional(left.constantValue, center.constantValue, right.constantValue, type);
    }

    internal static ConstantValue FoldConditional(
        ConstantValue left,
        ConstantValue center,
        ConstantValue right,
        TypeSymbol type) {
        var specialType = type.specialType;

        if (ConstantValue.IsNotNull(left) &&
            (bool)left.value &&
            center is not null) {
            return new ConstantValue(center.value, specialType);
        }

        if (ConstantValue.IsNotNull(left) &&
            !(bool)left.value &&
            right is not null) {
            return new ConstantValue(right.value, specialType);
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundCastExpression" /> (if possible).
    /// </summary>
    /// <param name="expression">Expression operand.</param>
    /// <param name="type">Casting to type.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldCast(
        BoundExpression expression,
        TypeWithAnnotations type,
        BelteDiagnosticQueue diagnostics) {
        return FoldCast(expression.constantValue, expression.syntax.location, expression.type, type, diagnostics);
    }

    internal static ConstantValue FoldCast(
        ConstantValue constantValue,
        TextLocation location,
        TypeSymbol source,
        TypeWithAnnotations target,
        BelteDiagnosticQueue diagnostics) {
        if (constantValue is null)
            return null;

        if (constantValue.value is null && !target.isNullable)
            return null;

        var targetType = target.type.StrippedType();

        if (targetType.IsEnumType())
            targetType = ((NamedTypeSymbol)targetType).enumUnderlyingType;

        var specialType = targetType.specialType;

        // Preserve "actual" type
        if (specialType == SpecialType.Any)
            return constantValue;

        try {
            if (LiteralUtilities.TryCast(constantValue.value, source, target, location, diagnostics, out var castedValue))
                return new ConstantValue(castedValue, specialType);
        } catch (Exception e) when (e is OverflowException or InvalidCastException) {
            diagnostics.Push(Error.CannotConvertConstantValue(location, constantValue.value, target.type));
        }

        return null;
    }

    /// <summary>
    /// Folds a <see cref="BoundInitializerListExpression" /> (if possible).
    /// </summary>
    /// <param name="items">Initializer list contents.</param>
    /// <returns><see cref="ConstantValue" />, returns null if folding is not possible.</returns>
    internal static ConstantValue FoldInitializerList(BoundInitializerList list) {
        var foldedItems = ArrayBuilder<ConstantValue>.GetInstance();

        foreach (var item in list.items) {
            if (item.constantValue is not null)
                foldedItems.Add(item.constantValue);
            else
                return null;
        }

        return new ConstantValue(foldedItems.ToImmutableAndFree(), SpecialType.Array);
    }

    /// <summary>
    /// Folds a <see cref="BoundArrayAccessExpression"/> (if possible).
    /// </summary>
    /// <param name="expression">The expression being indexed.</param>
    /// <param name="index">The index.</param>
    /// <returns>The constant item at the index, if constant.</returns>
    internal static ConstantValue FoldIndex(BoundExpression expression, BoundExpression index, TypeSymbol type) {
        var expressionConstant = expression.constantValue;
        var indexConstant = index.constantValue;

        if (expressionConstant is null || indexConstant is null)
            return null;

        if (type.specialType == SpecialType.Char && indexConstant is not null)
            return new ConstantValue(((string)expressionConstant.value)[Convert.ToInt32(indexConstant.value)], type.specialType);

        var array = (ImmutableArray<ConstantValue>)expressionConstant.value;
        var item = array[Convert.ToInt32(indexConstant.value)];
        var specialType = type.specialType;

        return new ConstantValue(item.value, specialType);
    }
}
