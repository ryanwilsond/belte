using System;
using System.Numerics;

namespace Buckle.Utils;

/// <summary>
/// Extensions on the System.Object class.
/// </summary>
public static class ObjectExtensions {
    /// <summary>
    /// Checks if the type of this is any floating point number (implements IFloatingPoint<>).
    /// Main examples are System.Double, System.Single, and System.Decimal.
    /// Note: The value of this is ignored, so (double)1 would still return true.
    /// </summary>
    /// <returns>True if the type of this is a floating point</returns>
    public static bool IsFloatingPoint(this object obj) {
        foreach (Type t in obj.GetType().GetInterfaces())
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IFloatingPoint<>))
                return true;

        return false;
    }
}
