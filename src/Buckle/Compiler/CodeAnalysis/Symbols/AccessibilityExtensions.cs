
namespace Buckle.CodeAnalysis.Symbols;

internal static class AccessibilityExtensions {
    internal static bool HasProtected(this Accessibility accessibility) {
        return accessibility switch {
            Accessibility.Protected => true,
            _ => false,
        };
    }
}
