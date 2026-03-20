
namespace Buckle.CodeAnalysis.Symbols;

internal static class MethodSymbolExtensions {
    internal static bool HasUnscopedRefAttributeOnMethod(this MethodSymbol method) {
        if (method is null)
            return false;

        return method.hasUnscopedRefAttribute;
    }

    internal static bool IsConstructor(this MethodSymbol method) {
        switch (method.methodKind) {
            case MethodKind.Constructor:
            case MethodKind.StaticConstructor:
                return true;
            default:
                return false;
        }
    }
}
