using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace Buckle.CodeAnalysis;

internal sealed class FileReferenceResolver : MetadataReferenceResolver, IEquatable<FileReferenceResolver> {
    private readonly Dictionary<(string, string, MetadataReferenceProperties), PortableExecutableReference> _referenceCache;

    internal FileReferenceResolver() {
        // This will usually either be small (~5), or very large (>50)
        // So let's just prepare for the larger case
        _referenceCache = new(capacity: 64);
    }

    internal override ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string baseFilePath,
        MetadataReferenceProperties properties) {
        if (_referenceCache.TryGetValue((reference, baseFilePath, properties), out var value))
            return [value];

        // TODO Do more with the baseFilePath, perhaps do some relative path work from the command line
        var path = reference;

        // Command line should verify this
        Debug.Assert(File.Exists(path));

        var peStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var peReference = MetadataReference.CreateFromFile(
            peStream,
            path,
            PEStreamOptions.PrefetchEntireImage,
            properties
        );

        _referenceCache.Add((reference, baseFilePath, properties), peReference);

        return [peReference];
    }

    public override int GetHashCode() {
        throw new NotImplementedException();
    }

    public bool Equals(FileReferenceResolver other) {
        throw new NotImplementedException();
    }

    public override bool Equals(object obj) {
        return obj is FileReferenceResolver other && Equals(other);
    }
}
