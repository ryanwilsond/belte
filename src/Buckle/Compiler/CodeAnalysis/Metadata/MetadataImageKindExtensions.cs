
namespace Buckle.CodeAnalysis;

internal static class MetadataImageKindExtensions {
    internal static bool IsValid(this MetadataImageKind kind) {
        return kind >= MetadataImageKind.Assembly && kind <= MetadataImageKind.Module;
    }
}
