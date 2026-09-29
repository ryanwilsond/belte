using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal abstract class MetadataReferenceResolver {
    public abstract override bool Equals(object other);

    public abstract override int GetHashCode();

    internal abstract ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string baseFilePath,
        MetadataReferenceProperties properties
    );

    internal virtual bool resolveMissingAssemblies => false;

    internal virtual PortableExecutableReference ResolveMissingAssembly(
        MetadataReference definition,
        AssemblyIdentity referenceIdentity) {
        return null;
    }
}
