
namespace Buckle.CodeAnalysis.Binding;

internal static class BoundNodeExtensions {
    internal static bool HasErrors(this BoundNode node) {
        return node is not null && node.hasErrors;
    }
}
