using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Utilities helping casting values, and utilities related to the <see cref="Convert" /> class.
/// </summary>
internal static class LiteralUtilities {
    internal static bool TryCast(object value, TypeWithAnnotations targetType, out object result) {
        if (value is null && !targetType.isNullable) {
            result = null;
            return false;
        }

        if (value is null) {
            result = null;
            return true;
        }

        return TryCast(value, targetType.type.StrippedType(), out result);
    }

    internal static bool TryCast(object value, TypeSymbol targetType, out object result) {
        try {
            result = Cast(value, targetType.specialType);
            return true;
        } catch (FormatException) {
            // TODO consider raising a diagnostic in this case
            result = null;
            return false;
        }
    }

    internal static object Cast(object value, SpecialType specialType) {
        // Prevents bankers rounding from Convert.ToInt64, instead always truncate (no rounding)
        if (value.IsFloatingPoint()) {
            switch (specialType) {
                case SpecialType.Int:
                case SpecialType.Int8:
                case SpecialType.Int16:
                case SpecialType.Int32:
                case SpecialType.Int64:
                case SpecialType.UInt8:
                case SpecialType.UInt16:
                case SpecialType.UInt32:
                case SpecialType.UInt64:
                    value = Math.Truncate(Convert.ToDouble(value));
                    break;
            }
        }

        switch (specialType) {
            case SpecialType.Bool:
                return Convert.ToBoolean(value);
            case SpecialType.Int8:
                return Convert.ToSByte(value);
            case SpecialType.Int16:
                return Convert.ToInt16(value);
            case SpecialType.Int32:
                return Convert.ToInt32(value);
            case SpecialType.UInt8:
                return Convert.ToByte(value);
            case SpecialType.UInt16:
                return Convert.ToUInt16(value);
            case SpecialType.UInt32:
                return Convert.ToUInt32(value);
            case SpecialType.UInt64:
                return Convert.ToUInt64(value);
            case SpecialType.Int:
            case SpecialType.Int64:
                return Convert.ToInt64(value);
            case SpecialType.Decimal:
            case SpecialType.Float64:
                return Convert.ToDouble(value);
            case SpecialType.Float32:
                return Convert.ToSingle(value);
            case SpecialType.String:
                return Convert.ToString(value);
            case SpecialType.Char:
                return Convert.ToChar(value);
            default:
                return value;
        }
    }

    internal static object ReduceNumeric(object value) {
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

        // TODO Do we want to reduce to unsigned?
        // if (dec >= byte.MinValue && dec <= byte.MaxValue) return (byte)dec;
        if (dec >= sbyte.MinValue && dec <= sbyte.MaxValue) return (sbyte)dec;
        if (dec >= short.MinValue && dec <= short.MaxValue) return (short)dec;
        // if (dec >= ushort.MinValue && dec <= ushort.MaxValue) return (ushort)dec;
        if (dec >= int.MinValue && dec <= int.MaxValue) return (int)dec;
        // if (dec >= uint.MinValue && dec <= uint.MaxValue) return (uint)dec;
        if (dec >= long.MinValue && dec <= long.MaxValue) return (long)dec;

        // return (ulong)dec;
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
