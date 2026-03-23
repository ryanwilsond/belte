
namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypeExtensions {
    internal static bool IsPrimitiveType(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Any:
            case SpecialType.String:
            case SpecialType.Bool:
            case SpecialType.Int:
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
            long => SpecialType.Int64,
            ulong => SpecialType.UInt64,
            float => SpecialType.Float32,
            double => SpecialType.Float64,
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
}
