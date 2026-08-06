using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace Buckle.CodeAnalysis;

internal abstract class MetadataReference {
    internal MetadataReferenceProperties properties { get; }

    private protected MetadataReference(MetadataReferenceProperties properties) {
        this.properties = properties;
    }

    internal virtual string display => null;

    internal virtual bool isUnresolved => false;

    internal static MetadataImageReference CreateFromFile(
        Stream peStream,
        string path,
        PEStreamOptions options,
        MetadataReferenceProperties properties) {
        var module = ModuleMetadata.CreateFromStream(peStream, options);

        if (properties.kind == MetadataImageKind.Module)
            return new MetadataImageReference(module, properties, path, display: null);

        var assemblyMetadata = AssemblyMetadata.CreateFromFile(module, path);
        return new MetadataImageReference(assemblyMetadata, properties, path, display: null);
    }

    internal MetadataReference WithAliases(IEnumerable<string> aliases) {
        return WithAliases(ImmutableArray.CreateRange(aliases));
    }

    internal MetadataReference WithEmbedInteropTypes(bool value) {
        return WithProperties(properties.WithEmbedInteropTypes(value));
    }

    internal MetadataReference WithAliases(ImmutableArray<string> aliases) {
        return WithProperties(properties.WithAliases(aliases));
    }

    internal MetadataReference WithProperties(MetadataReferenceProperties properties) {
        if (properties == this.properties)
            return this;

        return WithPropertiesImplReturningMetadataReference(properties);
    }

    internal abstract MetadataReference WithPropertiesImplReturningMetadataReference(
        MetadataReferenceProperties properties);

    internal static string GetAssemblyFilePath(
        Assembly assembly,
        MetadataReferenceProperties properties) {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly.IsDynamic)
            throw new NotSupportedException("CantCreateReferenceToDynamicAssembly");

        if (properties.kind != MetadataImageKind.Assembly)
            throw new ArgumentException("CantCreateModuleReferenceToAssembly", nameof(properties));

        var location = assembly.Location;

        if (string.IsNullOrEmpty(location))
            throw new NotSupportedException("CantCreateReferenceToAssemblyWithoutLocation");

        return location;
    }

    internal static bool HasMetadata(Assembly assembly) {
        return !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location);
    }
}
