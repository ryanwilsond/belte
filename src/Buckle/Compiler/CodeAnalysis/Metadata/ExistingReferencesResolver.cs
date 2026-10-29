using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal sealed class ExistingReferencesResolver : MetadataReferenceResolver, IEquatable<ExistingReferencesResolver> {
    private readonly MetadataReferenceResolver _resolver;
    private readonly ImmutableArray<MetadataReference> _availableReferences;
    private readonly Lazy<HashSet<AssemblyIdentity>> _lazyAvailableReferences;

    internal ExistingReferencesResolver(
        MetadataReferenceResolver resolver,
        ImmutableArray<MetadataReference> availableReferences) {
        Debug.Assert(resolver is not null);
        // Intentionally using != here
        Debug.Assert(availableReferences != null);

        _resolver = resolver;
        _availableReferences = availableReferences;

        _lazyAvailableReferences = new Lazy<HashSet<AssemblyIdentity>>(() => new HashSet<AssemblyIdentity>(
            from reference in _availableReferences
            let identity = TryGetIdentity(reference)
            where identity is not null
            select identity)
        );
    }

    internal override ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string baseFilePath,
        MetadataReferenceProperties properties) {
        var resolvedReferences = _resolver.ResolveReference(reference, baseFilePath, properties);
        return resolvedReferences.WhereAsArray(r => _lazyAvailableReferences.Value.Contains(TryGetIdentity(r)));
    }

    private static AssemblyIdentity TryGetIdentity(MetadataReference metadataReference) {
        if (metadataReference is not PortableExecutableReference peReference ||
            peReference.properties.kind != MetadataImageKind.Assembly) {
            return null;
        }

        try {
            var assembly = ((AssemblyMetadata)peReference.GetMetadataNoCopy()).GetAssembly()!;
            return assembly.identity;
        } catch (Exception e) when (e is BadImageFormatException || e is IOException) {
            return null;
        }
    }

    public override int GetHashCode() {
        return _resolver.GetHashCode();
    }

    public bool Equals(ExistingReferencesResolver? other) {
        return
            other is not null &&
            _resolver.Equals(other._resolver) &&
            _availableReferences.SequenceEqual(other._availableReferences);
    }

    public override bool Equals(object other) {
        return other is ExistingReferencesResolver obj && Equals(obj);
    }
}
