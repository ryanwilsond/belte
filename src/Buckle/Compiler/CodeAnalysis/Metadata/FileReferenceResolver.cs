using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace Buckle.CodeAnalysis;

internal sealed class FileReferenceResolver : MetadataReferenceResolver, IEquatable<FileReferenceResolver> {
    internal FileReferenceResolver() { }

    internal override ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string baseFilePath,
        MetadataReferenceProperties properties) {
        // TODO Do more with the baseFilePath, perhaps do some relative path work from the command line
        var path = reference;

        // Command line should verify this
        Debug.Assert(File.Exists(path));

        var peStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        return [MetadataReference.CreateFromFile(
            peStream,
            path,
            PEStreamOptions.PrefetchEntireImage,
            properties
        )];
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
