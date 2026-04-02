using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Utilities helping casting values, and utilities related to the <see cref="Convert" /> class.
/// </summary>
internal static class LiteralUtilities {
    internal static bool TryCast(
        object value,
        TypeSymbol source,
        TypeWithAnnotations targetType,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics,
        out object result) {
        if (value is null && !targetType.isNullable) {
            result = null;
            return false;
        }

        if (value is null) {
            result = null;
            return true;
        }

        return TryCast(value, source, targetType.type, errorLocation, diagnostics, out result);
    }

    internal static bool TryCast(
        object value,
        TypeSymbol sourceType,
        TypeSymbol targetType,
        TextLocation errorLocation,
        BelteDiagnosticQueue diagnostics,
        out object result) {
        try {
            return TryCastCore(value, sourceType, targetType, out result);
        } catch (FormatException) {
            diagnostics.Push(Error.CannotConvertConstantValue(errorLocation, value, targetType));
            result = null;
            return false;
        }
    }

    internal static bool TryCastCore(
        object value,
        TypeSymbol sourceTypeSymbol,
        TypeSymbol targetTypeSymbol,
        out object result) {
        if (sourceTypeSymbol.IsEnumType())
            sourceTypeSymbol = ((NamedTypeSymbol)sourceTypeSymbol).enumUnderlyingType;

        if (targetTypeSymbol.IsEnumType())
            targetTypeSymbol = ((NamedTypeSymbol)targetTypeSymbol).enumUnderlyingType;

        var sourceType = sourceTypeSymbol.StrippedType().specialType;
        var targetType = targetTypeSymbol.StrippedType().specialType;

        switch (targetType) {
            case SpecialType.Bool:
                result = Convert.ToBoolean(value);
                break;
            case SpecialType.String:
                result = Convert.ToString(value);
                break;
            case SpecialType.Int8:
                result = sourceType switch {
                    SpecialType.Int8 => value,
                    SpecialType.Int16 => unchecked((sbyte)(short)value),
                    SpecialType.Int32 => unchecked((sbyte)(int)value),
                    SpecialType.Int64 => unchecked((sbyte)(long)value),
                    SpecialType.Int => unchecked((sbyte)(long)value),
                    SpecialType.UInt8 => unchecked((sbyte)(byte)value),
                    SpecialType.UInt16 => unchecked((sbyte)(ushort)value),
                    SpecialType.UInt32 => unchecked((sbyte)(uint)value),
                    SpecialType.UInt64 => unchecked((sbyte)(ulong)value),
                    SpecialType.Float32 => unchecked((sbyte)(float)value),
                    SpecialType.Float64 => unchecked((sbyte)(double)value),
                    SpecialType.Decimal => unchecked((sbyte)(double)value),
                    SpecialType.String => Convert.ToSByte(value),
                    SpecialType.Char => unchecked((sbyte)(char)value),
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Int16:
                result = sourceType switch {
                    SpecialType.Int8 => (short)(sbyte)value,
                    SpecialType.Int16 => value,
                    SpecialType.Int32 => unchecked((short)(int)value),
                    SpecialType.Int64 => unchecked((short)(long)value),
                    SpecialType.Int => unchecked((short)(long)value),
                    SpecialType.UInt8 => (short)(byte)value,
                    SpecialType.UInt16 => unchecked((short)(ushort)value),
                    SpecialType.UInt32 => unchecked((short)(uint)value),
                    SpecialType.UInt64 => unchecked((short)(ulong)value),
                    SpecialType.Float32 => unchecked((short)(float)value),
                    SpecialType.Float64 => unchecked((short)(double)value),
                    SpecialType.Decimal => unchecked((short)(double)value),
                    SpecialType.String => Convert.ToInt16(value),
                    SpecialType.Char => unchecked((short)(char)value),
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Int32:
                result = sourceType switch {
                    SpecialType.Int8 => (int)(sbyte)value,
                    SpecialType.Int16 => (int)(short)value,
                    SpecialType.Int32 => value,
                    SpecialType.Int64 => unchecked((int)(long)value),
                    SpecialType.Int => unchecked((int)(long)value),
                    SpecialType.UInt8 => (int)(byte)value,
                    SpecialType.UInt16 => (int)(ushort)value,
                    SpecialType.UInt32 => unchecked((int)(uint)value),
                    SpecialType.UInt64 => unchecked((int)(ulong)value),
                    SpecialType.Float32 => unchecked((int)(float)value),
                    SpecialType.Float64 => unchecked((int)(double)value),
                    SpecialType.Decimal => unchecked((int)(double)value),
                    SpecialType.String => Convert.ToInt32(value),
                    SpecialType.Char => (int)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.UInt8:
                result = sourceType switch {
                    SpecialType.Int8 => unchecked((byte)(sbyte)value),
                    SpecialType.Int16 => unchecked((byte)(short)value),
                    SpecialType.Int32 => unchecked((byte)(int)value),
                    SpecialType.Int64 => unchecked((byte)(long)value),
                    SpecialType.Int => unchecked((byte)(long)value),
                    SpecialType.UInt8 => value,
                    SpecialType.UInt16 => unchecked((byte)(ushort)value),
                    SpecialType.UInt32 => unchecked((byte)(uint)value),
                    SpecialType.UInt64 => unchecked((byte)(ulong)value),
                    SpecialType.Float32 => unchecked((byte)(float)value),
                    SpecialType.Float64 => unchecked((byte)(double)value),
                    SpecialType.Decimal => unchecked((byte)(double)value),
                    SpecialType.String => Convert.ToByte(value),
                    SpecialType.Char => unchecked((byte)(char)value),
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.UInt16:
                result = sourceType switch {
                    SpecialType.Int8 => unchecked((ushort)(sbyte)value),
                    SpecialType.Int16 => unchecked((ushort)(short)value),
                    SpecialType.Int32 => unchecked((ushort)(int)value),
                    SpecialType.Int64 => unchecked((ushort)(long)value),
                    SpecialType.Int => unchecked((ushort)(long)value),
                    SpecialType.UInt8 => (ushort)(byte)value,
                    SpecialType.UInt16 => value,
                    SpecialType.UInt32 => unchecked((ushort)(uint)value),
                    SpecialType.UInt64 => unchecked((ushort)(ulong)value),
                    SpecialType.Float32 => unchecked((ushort)(float)value),
                    SpecialType.Float64 => unchecked((ushort)(double)value),
                    SpecialType.Decimal => unchecked((ushort)(double)value),
                    SpecialType.String => Convert.ToUInt16(value),
                    SpecialType.Char => (ushort)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.UInt32:
                result = sourceType switch {
                    SpecialType.Int8 => unchecked((uint)(sbyte)value),
                    SpecialType.Int16 => unchecked((uint)(short)value),
                    SpecialType.Int32 => unchecked((uint)(int)value),
                    SpecialType.Int64 => unchecked((uint)(long)value),
                    SpecialType.Int => unchecked((uint)(long)value),
                    SpecialType.UInt8 => (uint)(byte)value,
                    SpecialType.UInt16 => (uint)(ushort)value,
                    SpecialType.UInt32 => value,
                    SpecialType.UInt64 => unchecked((uint)(ulong)value),
                    SpecialType.Float32 => unchecked((uint)(float)value),
                    SpecialType.Float64 => unchecked((uint)(double)value),
                    SpecialType.Decimal => unchecked((uint)(double)value),
                    SpecialType.String => Convert.ToUInt32(value),
                    SpecialType.Char => (uint)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.UInt64:
                result = sourceType switch {
                    SpecialType.Int8 => unchecked((ulong)(sbyte)value),
                    SpecialType.Int16 => unchecked((ulong)(short)value),
                    SpecialType.Int32 => unchecked((ulong)(int)value),
                    SpecialType.Int64 => unchecked((ulong)(long)value),
                    SpecialType.Int => unchecked((ulong)(long)value),
                    SpecialType.UInt8 => (ulong)(byte)value,
                    SpecialType.UInt16 => (ulong)(ushort)value,
                    SpecialType.UInt32 => (ulong)(uint)value,
                    SpecialType.UInt64 => value,
                    SpecialType.Float32 => unchecked((ulong)(float)value),
                    SpecialType.Float64 => unchecked((ulong)(double)value),
                    SpecialType.Decimal => unchecked((ulong)(double)value),
                    SpecialType.String => Convert.ToUInt64(value),
                    SpecialType.Char => (ulong)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Int:
            case SpecialType.Int64:
                result = sourceType switch {
                    SpecialType.Int8 => (long)(sbyte)value,
                    SpecialType.Int16 => (long)(short)value,
                    SpecialType.Int32 => (long)(int)value,
                    SpecialType.Int64 => value,
                    SpecialType.Int => value,
                    SpecialType.UInt8 => (long)(byte)value,
                    SpecialType.UInt16 => (long)(ushort)value,
                    SpecialType.UInt32 => (long)(uint)value,
                    SpecialType.UInt64 => unchecked((long)(ulong)value),
                    SpecialType.Float32 => unchecked((long)(float)value),
                    SpecialType.Float64 => unchecked((long)(double)value),
                    SpecialType.Decimal => unchecked((long)(double)value),
                    SpecialType.String => Convert.ToInt64(value),
                    SpecialType.Char => (long)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Decimal:
            case SpecialType.Float64:
                result = sourceType switch {
                    SpecialType.Int8 => (double)(sbyte)value,
                    SpecialType.Int16 => (double)(short)value,
                    SpecialType.Int32 => (double)(int)value,
                    SpecialType.Int64 => unchecked((double)(long)value),
                    SpecialType.Int => unchecked((double)(long)value),
                    SpecialType.UInt8 => (double)(byte)value,
                    SpecialType.UInt16 => (double)(ushort)value,
                    SpecialType.UInt32 => (double)(uint)value,
                    SpecialType.UInt64 => unchecked((double)(ulong)value),
                    SpecialType.Float32 => (double)(float)value,
                    SpecialType.Float64 => value,
                    SpecialType.Decimal => value,
                    SpecialType.String => Convert.ToDouble(value),
                    SpecialType.Char => (double)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Float32:
                result = sourceType switch {
                    SpecialType.Int8 => (float)(sbyte)value,
                    SpecialType.Int16 => (float)(short)value,
                    SpecialType.Int32 => unchecked((float)(int)value),
                    SpecialType.Int64 => unchecked((float)(long)value),
                    SpecialType.Int => unchecked((float)(long)value),
                    SpecialType.UInt8 => (float)(byte)value,
                    SpecialType.UInt16 => (float)(ushort)value,
                    SpecialType.UInt32 => unchecked((float)(uint)value),
                    SpecialType.UInt64 => unchecked((float)(ulong)value),
                    SpecialType.Float32 => value,
                    SpecialType.Float64 => unchecked((float)(double)value),
                    SpecialType.Decimal => unchecked((float)(double)value),
                    SpecialType.String => Convert.ToSingle(value),
                    SpecialType.Char => (float)(char)value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            case SpecialType.Char:
                result = sourceType switch {
                    SpecialType.Int8 => unchecked((char)(sbyte)value),
                    SpecialType.Int16 => unchecked((char)(short)value),
                    SpecialType.Int32 => unchecked((char)(int)value),
                    SpecialType.Int64 => unchecked((char)(long)value),
                    SpecialType.Int => unchecked((char)(long)value),
                    SpecialType.UInt8 => (char)(byte)value,
                    SpecialType.UInt16 => (char)(ushort)value,
                    SpecialType.UInt32 => unchecked((char)(uint)value),
                    SpecialType.UInt64 => unchecked((char)(ulong)value),
                    SpecialType.Float32 => unchecked((char)(float)value),
                    SpecialType.Float64 => unchecked((char)(double)value),
                    SpecialType.Decimal => unchecked((char)(double)value),
                    SpecialType.String => Convert.ToChar(value),
                    SpecialType.Char => value,
                    _ => throw ExceptionUtilities.UnexpectedValue(sourceType),
                };

                break;
            default:
                result = null;
                return false;
        }

        return true;
    }

    internal static object ReduceNumeric(object value, bool unsigned) {
        // TODO We handle this during binding, maybe the Lexer should instead?
        if (value is null)
            return null;

        double? nDec = value switch {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => ul,
            float f => (double)f,
            double db => db,
            _ => null
        };

        if (!nDec.HasValue)
            return value;

        var dec = nDec.Value;

        if (dec % 1 != 0) {
            if (dec >= (double)float.MinValue && dec <= (double)float.MaxValue &&
                Convert.ToSingle(dec) == dec) {
                return (float)dec;
            }

            return (double)dec;
        }

        if (unsigned && dec >= byte.MinValue && dec <= byte.MaxValue) return (byte)dec;
        if (!unsigned && dec >= sbyte.MinValue && dec <= sbyte.MaxValue) return (sbyte)dec;
        if (!unsigned && dec >= short.MinValue && dec <= short.MaxValue) return (short)dec;
        if (unsigned && dec >= ushort.MinValue && dec <= ushort.MaxValue) return (ushort)dec;
        if (!unsigned && dec >= int.MinValue && dec <= int.MaxValue) return (int)dec;
        if (unsigned && dec >= uint.MinValue && dec <= uint.MaxValue) return (uint)dec;
        if (!unsigned && dec >= long.MinValue && dec <= long.MaxValue) return (long)dec;
        if (unsigned && dec >= ulong.MinValue && dec <= ulong.MaxValue) return (ulong)dec;

        return dec;
    }

    internal static object GetDefaultValue(SpecialType type) {
        return type switch {
            SpecialType.Int => 0L,
            SpecialType.Decimal => 0D,
            SpecialType.Int8 => (sbyte)0,
            SpecialType.Int16 => (short)0,
            SpecialType.Int32 => 0,
            SpecialType.Int64 => 0L,
            SpecialType.UInt8 => (byte)0,
            SpecialType.UInt16 => (ushort)0,
            SpecialType.UInt32 => 0U,
            SpecialType.UInt64 => 0UL,
            SpecialType.Float32 => 0F,
            SpecialType.Float64 => 0D,
            SpecialType.Bool => false,
            SpecialType.Char => '\0',
            _ => throw ExceptionUtilities.UnexpectedValue(type)
        };
    }
}
