using System.Collections.Generic;
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

        if (left.typeSymbol is not ClassSymbol c || right.typeSymbol is not ClassSymbol || c.baseType is null)
            return false;

        var baseType = (left.typeSymbol as ClassSymbol).baseType;

        if (left.templateArguments.Length > 0) {
            var templateMappings = new Dictionary<ParameterSymbol, TypeOrConstant>();

            for (var i = 0; i < left.templateArguments.Length; i++) {
                templateMappings.Add(
                    (left.typeSymbol as NamedTypeSymbol).templateParameters[i],
                    left.templateArguments[i]
                );
            }

            baseType = BoundType.Clarify(baseType, templateMappings);
        }

        return TypeInheritsFrom(baseType, right);
    }

    /// <summary>
    /// Checks if a type is or inherits from another.
    /// </summary>
    /// <param name="left">The type being tested.</param>
    /// <param name="right">The base type.</param>
    /// <returns>If the left type inherits from or is the right type.</returns>
    internal static bool TypeInheritsFrom(TypeSymbol left, TypeSymbol right) {
        if (left == right)
            return true;

        if (left is not ClassSymbol c || right is not ClassSymbol || c.baseType is null)
            return false;

        return TypeInheritsFrom((left as ClassSymbol).baseType.typeSymbol, right);
    }

    /// <summary>
    /// Gets the distance on the inheritance tree between two types. If the types are the same, the depth is zero.
    /// Assumes that the types connect on the inheritance tree. Otherwise, zero.
    /// Assumes <paramref name="left"/> is a child of <paramref name="right"/>.
    /// </summary>
    internal static int GetInheritanceDepth(TypeSymbol left, TypeSymbol right) {
        if (left == right || left is not ClassSymbol c || right is not ClassSymbol || c.baseType is null)
            return 0;

        var depth = 0;
        var current = left as ClassSymbol;

        do {
            depth++;
            current = current.baseType?.typeSymbol as ClassSymbol;
        } while (current is not null);

        return depth;
    }
}
