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
        switch (specialType) {
            case SpecialType.Bool:
                return Convert.ToBoolean(value);
            case SpecialType.Int:
                // Prevents bankers rounding from Convert.ToInt64, instead always truncate (no rounding)
                if (value.IsFloatingPoint())
                    value = Math.Truncate(Convert.ToDouble(value));

                return Convert.ToInt64(value);
            case SpecialType.Decimal:
                return Convert.ToDouble(value);
            case SpecialType.String:
                return Convert.ToString(value);
            case SpecialType.Char:
                return Convert.ToChar(value);
            default:
                return value;
        }
    }

    internal static SpecialType AssumeTypeFromLiteral(object value) {
        if (value is bool)
            return SpecialType.Bool;

        if (value is long)
            return SpecialType.Int;

        if (value is string)
            return SpecialType.String;

        if (value is char)
            return SpecialType.Char;

        if (value is double)
            return SpecialType.Decimal;

        if (value is TypeSymbol)
            return SpecialType.Type;

        return SpecialType.None;
    }

    internal static object GetDefaultValue(SpecialType type) {
        return type switch {
            SpecialType.Int => 0L,
            SpecialType.Decimal => 0D,
            SpecialType.Bool => false,
            SpecialType.Char => '\0',
            _ => throw ExceptionUtilities.UnexpectedValue(type)
        };
    }
}
