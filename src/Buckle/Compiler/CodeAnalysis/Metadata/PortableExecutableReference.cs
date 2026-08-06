using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal abstract class PortableExecutableReference : MetadataReference {
    private protected PortableExecutableReference(
        MetadataReferenceProperties properties,
        string fullPath = null)
        : base(properties) {
        filePath = fullPath;
    }

    internal override string display => filePath;

    internal string filePath { get; }

    internal new PortableExecutableReference WithAliases(IEnumerable<string> aliases) {
        return WithAliases(ImmutableArray.CreateRange(aliases));
    }

    internal new PortableExecutableReference WithAliases(ImmutableArray<string> aliases) {
        return WithProperties(properties.WithAliases(aliases));
    }

    internal new PortableExecutableReference WithEmbedInteropTypes(bool value) {
        return WithProperties(properties.WithEmbedInteropTypes(value));
    }

    internal new PortableExecutableReference WithProperties(MetadataReferenceProperties properties) {
        if (properties == this.properties)
            return this;

        return WithPropertiesImpl(properties);
    }

    internal sealed override MetadataReference WithPropertiesImplReturningMetadataReference(
        MetadataReferenceProperties properties) {
        return WithPropertiesImpl(properties);
    }

    private protected abstract PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties);

    private protected abstract Metadata GetMetadataImpl();

    internal Metadata GetMetadataNoCopy() {
        return GetMetadataImpl();
    }

    internal Metadata GetMetadata() {
        return GetMetadataNoCopy().Copy();
    }

    internal MetadataId GetMetadataId() {
        return GetMetadataNoCopy().id;
    }
}
