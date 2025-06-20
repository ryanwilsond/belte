
namespace Buckle.CodeAnalysis;

internal sealed class MetadataId {
    private MetadataId() { }

    internal static MetadataId CreateNewId() => new MetadataId();
}
