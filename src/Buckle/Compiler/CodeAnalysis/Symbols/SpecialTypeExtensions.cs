
namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypeExtensions {
    internal static bool IsPrimitiveType(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Any:
            case SpecialType.String:
            case SpecialType.Bool:
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

    internal static bool IsValidEnumUnderlyingType(this SpecialType specialType) {
        return IsIntegral(specialType) || specialType == SpecialType.String || specialType == SpecialType.Char;
    }

    internal static bool IsObjectType(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Object:
            case SpecialType.Nullable:
            case SpecialType.List:
            case SpecialType.Dictionary:
            case SpecialType.Vec2:
            case SpecialType.Sprite:
                return true;
            default:
                return false;
        }
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
            default:
                return 0;
        }
    }
}
