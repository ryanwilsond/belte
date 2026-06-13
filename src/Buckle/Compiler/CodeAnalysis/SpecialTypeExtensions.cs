using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class SpecialTypeExtensions {
    internal static bool IsKnownToBeImmutable(this SpecialType specialType) {
        // This is only caring about reference types
        switch (specialType) {
            case SpecialType.Type:
            case SpecialType.String:
            // This is correct because this check does not care about derived types and Object has no fields:
            case SpecialType.Object:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsPrimitiveType(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Any:
            case SpecialType.String:
            case SpecialType.Bool:
            case SpecialType.WinBool:
            case SpecialType.Int:
            case SpecialType.Enum:
            case SpecialType.Decimal:
            case SpecialType.Type:
            case SpecialType.Char:
            case SpecialType.Array:
            case SpecialType.Int8:
            case SpecialType.UInt8:
            case SpecialType.Int16:
            case SpecialType.UInt16:
            case SpecialType.Int32:
            case SpecialType.UInt32:
            case SpecialType.Int64:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsReferenceType(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Array:
            case SpecialType.String:
            case SpecialType.Any:
            case SpecialType.Type:
            case SpecialType.Object:
            case SpecialType.Buffer:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsValueType(this SpecialType specialType) {
        return !IsReferenceType(specialType);
    }

    internal static bool IsValidEnumUnderlyingType(this SpecialType specialType) {
        return IsIntegral(specialType) || specialType == SpecialType.String || specialType == SpecialType.Char;
    }

    internal static bool CanOptimizeBehavior(this SpecialType specialType) {
        return specialType >= SpecialType.Object && specialType <= SpecialType.ValueType;
    }

    internal static bool IsUnsigned(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Char:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.UIntPtr:
                return true;
            default:
                return false;
        }
    }

    internal static SpecialType SpecialTypeFromLiteralValue(object value) {
        if (value is null)
            return SpecialType.None;

        return value switch {
            sbyte => SpecialType.Int8,
            byte => SpecialType.UInt8,
            short => SpecialType.Int16,
            ushort => SpecialType.UInt16,
            int => SpecialType.Int32,
            uint => SpecialType.UInt32,
            // long => SpecialType.Int64,
            long => SpecialType.Int,
            ulong => SpecialType.UInt64,
            float => SpecialType.Float32,
            // double => SpecialType.Float64,
            double => SpecialType.Decimal,
            string => SpecialType.String,
            bool => SpecialType.Bool,
            char => SpecialType.Char,
            TypeSymbol => SpecialType.Type,
            _ => SpecialType.None,
        };
    }

    internal static bool IsNumeric(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Int:
            case SpecialType.Decimal:
            case SpecialType.WinBool:
            case SpecialType.Char:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsFloatingPoint(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Decimal:
            case SpecialType.Float32:
            case SpecialType.Float64:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsIntegral(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Int:
            case SpecialType.Char:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.WinBool:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsLongIntegral(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Int:
            case SpecialType.Int64:
            case SpecialType.UInt64:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsLowLevelNumeric(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
                return true;
            default:
                return false;
        }
    }

    internal static int FixedBufferElementSizeInBytes(this SpecialType specialType) {
        return specialType.SizeInBytes();
    }

    internal static int SizeInBytes(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Int8:
                return sizeof(sbyte);
            case SpecialType.UInt8:
                return sizeof(byte);
            case SpecialType.Int16:
                return sizeof(short);
            case SpecialType.UInt16:
                return sizeof(ushort);
            case SpecialType.Int32:
                return sizeof(int);
            case SpecialType.UInt32:
                return sizeof(uint);
            case SpecialType.Int64:
            case SpecialType.Int:
                return sizeof(long);
            case SpecialType.UInt64:
                return sizeof(ulong);
            case SpecialType.Char:
                return sizeof(char);
            case SpecialType.Float32:
                return sizeof(float);
            case SpecialType.Float64:
            case SpecialType.Decimal:
                return sizeof(double);
            case SpecialType.Bool:
                return sizeof(bool);
            case SpecialType.WinBool:
                return sizeof(int);
            default:
                return 0;
        }
    }

    internal static bool HasShortFormSignatureEncoding(this SpecialType type) {
        switch (type) {
            case SpecialType.String:
            case SpecialType.Object:
            case SpecialType.Void:
            case SpecialType.Bool:
            case SpecialType.WinBool:
            case SpecialType.Char:
            case SpecialType.UInt8:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.UInt16:
            case SpecialType.Int32:
            case SpecialType.UInt32:
            case SpecialType.Int64:
            case SpecialType.UInt64:
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
            // TODO
            // case SpecialType.TypedReference:
            case SpecialType.Float32:
            case SpecialType.Float64:
                return true;
        }

        return false;
    }

    internal static bool IsValidExtendedLiteral(this SpecialType type) {
        switch (type) {
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.Int:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.Decimal:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.String:
            case SpecialType.Char:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsValidPointerExtendedLiteral(this SpecialType type) {
        switch (type) {
            case SpecialType.UInt8:
            case SpecialType.Char:
                return true;
            default:
                return false;
        }
    }
}
