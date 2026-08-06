using System.Reflection;

namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeAttributesExtensions {
    internal static bool IsInterface(this TypeAttributes flags) {
        return (flags & TypeAttributes.Interface) != 0;
    }
}
