using Buckle.Utilities;

namespace Buckle;

internal static class OutputKindExtensions {
    internal static bool HasEntryPoint(this OutputKind kind) {
        switch (kind) {
            case OutputKind.ConsoleApplication:
            case OutputKind.GraphicsApplication:
                return true;
            case OutputKind.DynamicallyLinkedLibrary:
                return false;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }
}
