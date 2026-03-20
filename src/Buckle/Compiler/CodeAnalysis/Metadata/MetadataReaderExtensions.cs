using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class MetadataReaderExtensions {
    internal static Guid GetModuleVersionIdOrThrow(this MetadataReader reader) {
        return reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }

    internal static bool DeclaresTheObjectClass(this MetadataReader reader) {
        return reader.DeclaresType(IsTheObjectClass);
    }

    private static bool IsTheObjectClass(this MetadataReader reader, TypeDefinition typeDef) {
        return typeDef.BaseType.IsNil &&
            reader.IsPublicNonInterfaceType(typeDef, "System", "Object");
    }

    internal static bool DeclaresType(this MetadataReader reader, Func<MetadataReader, TypeDefinition, bool> predicate) {
        foreach (var handle in reader.TypeDefinitions) {
            try {
                var typeDef = reader.GetTypeDefinition(handle);

                if (predicate(reader, typeDef))
                    return true;
            } catch (BadImageFormatException) { }
        }

        return false;
    }

    internal static bool IsPublicNonInterfaceType(
        this MetadataReader reader,
        TypeDefinition typeDef,
        string namespaceName,
        string typeName) {
        return (typeDef.Attributes & (TypeAttributes.Public | TypeAttributes.Interface)) == TypeAttributes.Public &&
            reader.StringComparer.Equals(typeDef.Name, typeName) &&
            reader.StringComparer.Equals(typeDef.Namespace, namespaceName);
    }

    internal static ImmutableArray<AssemblyIdentity> GetReferencedAssembliesOrThrow(this MetadataReader reader) {
        var result = ArrayBuilder<AssemblyIdentity>.GetInstance(reader.AssemblyReferences.Count);

        try {
            foreach (var assemblyRef in reader.AssemblyReferences) {
                var reference = reader.GetAssemblyReference(assemblyRef);
                result.Add(reader.CreateAssemblyIdentityOrThrow(
                    reference.Version,
                    reference.Flags,
                    reference.PublicKeyOrToken,
                    reference.Name,
                    reference.Culture,
                    isReference: true));
            }

            return result.ToImmutable();
        } finally {
            result.Free();
        }
    }

    private static AssemblyIdentity CreateAssemblyIdentityOrThrow(
        this MetadataReader reader,
        Version version,
        AssemblyFlags flags,
        BlobHandle publicKey,
        StringHandle name,
        StringHandle culture,
        bool isReference) {
        var nameStr = reader.GetString(name);
        // TODO Errors

        // if (!MetadataHelpers.IsValidMetadataIdentifier(nameStr)) {
        //     throw new BadImageFormatException(string.Format(CodeAnalysisResources.InvalidAssemblyName, nameStr));
        // }

        var cultureName = culture.IsNil ? null : reader.GetString(culture);

        // if (cultureName is not null && !MetadataHelpers.IsValidMetadataIdentifier(cultureName)) {
        //     throw new BadImageFormatException(string.Format(CodeAnalysisResources.InvalidCultureName, cultureName));
        // }

        var publicKeyOrToken = reader.GetBlobContent(publicKey);
        bool hasPublicKey;

        if (isReference) {
            hasPublicKey = (flags & AssemblyFlags.PublicKey) != 0;
            if (hasPublicKey) {
                // if (!MetadataHelpers.IsValidPublicKey(publicKeyOrToken)) {
                //     throw new BadImageFormatException(CodeAnalysisResources.InvalidPublicKey);
                // }
            } else {
                // if (!publicKeyOrToken.IsEmpty &&
                //     publicKeyOrToken.Length != AssemblyIdentity.PublicKeyTokenSize) {
                //     throw new BadImageFormatException(CodeAnalysisResources.InvalidPublicKeyToken);
                // }
            }
        } else {
            hasPublicKey = !publicKeyOrToken.IsEmpty;

            // if (hasPublicKey && !MetadataHelpers.IsValidPublicKey(publicKeyOrToken)) {
            //     throw new BadImageFormatException(CodeAnalysisResources.InvalidPublicKey);
            // }
        }

        if (publicKeyOrToken.IsEmpty)
            publicKeyOrToken = default;

        return new AssemblyIdentity(
            name: nameStr,
            version: version,
            cultureName: cultureName,
            publicKeyOrToken: publicKeyOrToken,
            hasPublicKey: hasPublicKey,
            isRetargetable: (flags & AssemblyFlags.Retargetable) != 0,
            contentType: (AssemblyContentType)((int)(flags & AssemblyFlags.ContentTypeMask) >> 9),
            noThrow: true
        );
    }

    internal static AssemblyIdentity ReadAssemblyIdentityOrThrow(this MetadataReader reader) {
        if (!reader.IsAssembly)
            return null;

        var assemblyDef = reader.GetAssemblyDefinition();

        return reader.CreateAssemblyIdentityOrThrow(
            assemblyDef.Version,
            assemblyDef.Flags,
            assemblyDef.PublicKey,
            assemblyDef.Name,
            assemblyDef.Culture,
            isReference: false
        );
    }
}
