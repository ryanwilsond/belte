
namespace Buckle.CodeAnalysis.Symbols;

internal static class DataContainerDeclarationKindExtensions {
    internal static bool IsFinal(this DataContainerDeclarationKind kind) {
        switch (kind) {
            case DataContainerDeclarationKind.Final:
            case DataContainerDeclarationKind.ForEachLocal:
            case DataContainerDeclarationKind.NullBindingLocal:
            case DataContainerDeclarationKind.ScopedLocal:
                return true;
            default:
                return false;
        }
    }
}
