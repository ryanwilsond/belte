
namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeParameterBoundsExtensions {
    internal static bool IsSet(this TypeParameterBounds bounds) {
        return bounds != TypeParameterBounds.Unset;
    }
}
