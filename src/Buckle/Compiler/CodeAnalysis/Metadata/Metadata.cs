using System;

namespace Buckle.CodeAnalysis;

internal abstract class Metadata : IDisposable {
    internal readonly bool isImageOwner;

    internal MetadataId id { get; }

    internal Metadata(bool isImageOwner, MetadataId id) {
        this.isImageOwner = isImageOwner;
        this.id = id;
    }

    internal abstract MetadataImageKind kind { get; }

    public abstract void Dispose();

    private protected abstract Metadata CommonCopy();

    public Metadata Copy() {
        return CommonCopy();
    }
}
