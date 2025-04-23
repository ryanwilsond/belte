
namespace Buckle.CodeAnalysis.Symbols;

internal static class MethodSymbolExtensions {
    internal static bool HasUnscopedRefAttributeOnMethod(this MethodSymbol method) {
        if (method is null)
            return false;

        return method.hasUnscopedRefAttribute;
    }
}
