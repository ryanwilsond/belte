
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

    internal static SpecialType SpecialTypeFromLiteralValue(object value) {
        if (value is null)
            return SpecialType.None;

        if (value.GetType() == typeof(long))
            return SpecialType.Int;

        if (value.GetType() == typeof(string))
            return SpecialType.String;

        if (value.GetType() == typeof(bool))
            return SpecialType.Bool;

        if (value.GetType() == typeof(char))
            return SpecialType.Char;

        if (value.GetType() == typeof(double))
            return SpecialType.Decimal;

        return SpecialType.None;
    }
}
