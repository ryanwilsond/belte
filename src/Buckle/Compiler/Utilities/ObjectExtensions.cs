using System.Numerics;

namespace Buckle.Utilities;

/// <summary>
/// Extensions on the System.Object class.
/// </summary>
internal static class ObjectExtensions {
    /// <summary>
    /// Checks if the type of this is any floating point number (implements IFloatingPoint<>).
    /// Main examples are System.Double, System.Single, and System.Decimal.
    /// Note: The value of this is ignored, so (double)1 would still return true.
    /// </summary>
    /// <returns>True if the type of this is a floating point.</returns>
    internal static bool IsFloatingPoint(this object self) {
        foreach (var t in self.GetType().GetInterfaces()) {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IFloatingPoint<>))
                return true;
        }

        return false;
    }
}
