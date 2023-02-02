using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
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
    internal static object Cast(object value, BoundType targetType) {
        if (value == null && !targetType.isNullable)
            throw new NullReferenceException();

        if (value == null)
            return null;

        var typeSymbol = targetType?.typeSymbol;

        if (typeSymbol == TypeSymbol.Bool) {
            return Convert.ToBoolean(value);
        } else if (typeSymbol == TypeSymbol.Int) {
            // Prevents bankers rounding from Convert.ToInt32, instead always truncate (no rounding)
            if (value.IsFloatingPoint())
                value = Math.Truncate(Convert.ToDouble(value));

            return Convert.ToInt32(value);
        } else if (typeSymbol == TypeSymbol.Decimal) {
            return Convert.ToDouble(value);
        } else if (typeSymbol == TypeSymbol.String) {
            return Convert.ToString(value);
        } else {
            return value;
        }
    }
}
