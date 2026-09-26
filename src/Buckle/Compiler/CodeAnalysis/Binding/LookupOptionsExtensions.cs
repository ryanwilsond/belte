
namespace Buckle.CodeAnalysis.Binding;

internal static class LookupOptionsExtensions {
    internal static bool CanConsiderMembers(this LookupOptions options) {
        return (options & LookupOptions.NamespacesOrTypesOnly) == 0;
    }

    internal static bool CanConsiderLocals(this LookupOptions options) {
        return (options & LookupOptions.NamespacesOrTypesOnly) == 0;
    }

    internal static bool IsAttributeTypeLookup(this LookupOptions options) {
        return (options & LookupOptions.AttributeTypeOnly) == LookupOptions.AttributeTypeOnly;
    }

    internal static bool IsVerbatimNameAttributeTypeLookup(this LookupOptions options) {
        return (options & LookupOptions.VerbatimNameAttributeTypeOnly) == LookupOptions.VerbatimNameAttributeTypeOnly;
    }
}
