using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Utilities;

/// <summary>
/// Utilities helping casting values, and utilities related to the <see cref="Convert" /> class.
/// </summary>
internal static class CastUtilities {
    /// <summary>
    /// Casts a value to another type based on the given target type.
    /// </summary>
    /// <param name="value">What to cast.</param>
    /// <param name="type">The target type of the value.</param>
    /// <returns>The casted value, does not wrap conversion exceptions.</returns>
    internal static object Cast(object value, TypeSymbol targetType) {
        if (targetType == TypeSymbol.Bool) {
            return Convert.ToBoolean(value);
        } else if (targetType == TypeSymbol.Int) {
            // Prevents bankers rounding from Convert.ToInt32, instead always truncate (no rounding)
            if (value.IsFloatingPoint())
                value = Math.Truncate(Convert.ToDouble(value));

            return Convert.ToInt32(value);
        } else if (targetType == TypeSymbol.Decimal) {
            return Convert.ToDouble(value);
        } else if (targetType == TypeSymbol.String) {
            return Convert.ToString(value);
        } else {
            return value;
        }
    }
}
