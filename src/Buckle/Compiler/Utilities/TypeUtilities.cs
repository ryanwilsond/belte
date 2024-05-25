using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Utilities;

internal static class TypeUtilities {
    /// <summary>
    /// Checks if a type is or inherits from another.
    /// </summary>
    /// <param name="left">The type being tested.</param>
    /// <param name="right">The base type.</param>
    /// <returns>If the left type inherits from or is the right type.</returns>
    internal static bool TypeInheritsFrom(BoundType left, BoundType right) {
        if (left.Equals(right, isTypeCheck: true))
            return true;

        if (left.typeSymbol is not ClassSymbol ||
            right.typeSymbol is not ClassSymbol ||
            (left.typeSymbol as ClassSymbol).baseType is null) {
            return false;
        }

        return TypeInheritsFrom((left.typeSymbol as ClassSymbol).baseType, right);
    }
}
