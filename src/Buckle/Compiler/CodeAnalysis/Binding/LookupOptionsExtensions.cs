
namespace Buckle.CodeAnalysis.Binding;

internal static class LookupOptionsExtensions {
    internal static bool CanConsiderMembers(this LookupOptions options) {
        return (options & LookupOptions.NamespacesOrTypesOnly) == 0;
    }

    internal static bool CanConsiderLocals(this LookupOptions options) {
        return (options & LookupOptions.NamespacesOrTypesOnly) == 0;
    }
}
