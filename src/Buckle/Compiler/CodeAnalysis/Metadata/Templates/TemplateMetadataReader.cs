using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    private readonly Compilation _compilation;
    private readonly Dictionary<AssemblyIdentity, TemplateMetadata> _templateMetadata = [];

    private Dictionary<AssemblyIdentity, byte[]> _rawTemplateMetadata;

    internal TemplateMetadataReader(Compilation compilation) {
        _compilation = compilation;
    }

    [Conditional("DEBUG")]
    internal void ForceComplete() {
        EnsureTemplateMetadataIsRead();

        foreach (var pair in _rawTemplateMetadata) {
            if (!_templateMetadata.TryGetValue(pair.Key, out var value)) {
                var templateMetadata = new TemplateMetadata(_compilation, pair.Value);
                templateMetadata.ForceComplete();
                _templateMetadata[pair.Key] = templateMetadata;
            } else {
                value.ForceComplete();
            }
        }
    }

    internal void AppendTemplateTypes(ArrayBuilder<NamedTypeSymbol> builder, PENamespaceSymbol ns) {
        EnsureTemplateMetadataIsRead();

        if (!TryGetTemplateMetadata(ns.containingAssembly.identity, out var templateMetadata))
            return;

        templateMetadata.CreateTemplateTypes(builder, ns);
    }

    internal bool HasMetadataForType(TypeSymbol type) {
        EnsureTemplateMetadataIsRead();

        if (!TryGetTemplateMetadata(type.containingAssembly.identity, out var templateMetadata))
            return false;

        return templateMetadata.HasTemplateEntryForType(type);
    }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> GetMethodMetadataForType(TypeSymbol type) {
        Debug.Assert(_rawTemplateMetadata is not null);

        if (!TryGetTemplateMetadata(type.containingAssembly.identity, out var templateMetadata))
            throw ExceptionUtilities.Unreachable();

        return templateMetadata.DecodeMethodsAndBodiesForType(type);
    }

    internal Symbol GetLinkedSymbol(Symbol symbol) {
        var type = (symbol as TypeSymbol) ?? symbol.containingType;

        if (type is null)
            return null;

        EnsureTemplateMetadataIsRead();

        if (!TryGetTemplateMetadata(type.containingAssembly.identity, out var templateMetadata))
            return null;

        if (!templateMetadata.HasTemplateEntryForType(type))
            return null;

        return templateMetadata.GetLinkedSymbol(type, symbol);
    }

    private void EnsureTemplateMetadataIsRead() {
        if (_rawTemplateMetadata is not null)
            return;

        var referenceManager = _compilation.GetBoundReferenceManager();

        var templateMetadata = new Dictionary<AssemblyIdentity, byte[]>();

        foreach (var metadata in _compilation.externalReferences) {
            if (metadata is not PortableExecutableReference peReference)
                continue;

            var path = peReference.filePath;
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);

            var metadataReader = peReader.GetMetadataReader();

            foreach (var resourceHandle in metadataReader.ManifestResources) {
                var resource = metadataReader.GetManifestResource(resourceHandle);
                var resourceName = metadataReader.GetString(resource.Name);

                if (string.Equals(
                    resourceName,
                    TemplateMetadataWriter.ResourceName,
                    System.StringComparison.Ordinal)) {
                    var assembly = referenceManager.referencedAssemblies[
                        referenceManager.referencedAssembliesMap[metadata]
                    ];

                    Debug.Assert(!templateMetadata.ContainsKey(assembly.identity));

                    var rva = peReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress;
                    var resourceBlock = peReader.GetSectionData(rva);

                    var offset = (int)resource.Offset;
                    var reader = resourceBlock.GetReader(offset, resourceBlock.Length - offset);

                    var length = reader.ReadUInt32();
                    var bytes = reader.ReadBytes((int)length);

                    templateMetadata.Add(assembly.identity, bytes);
                }
            }
        }

        Interlocked.CompareExchange(ref _rawTemplateMetadata, templateMetadata, null);
    }

    private bool TryGetTemplateMetadata(AssemblyIdentity assemblyIdentity, out TemplateMetadata templateMetadata) {
        if (!_rawTemplateMetadata.TryGetValue(assemblyIdentity, out var bytes)) {
            templateMetadata = null;
            return false;
        }

        if (!_templateMetadata.TryGetValue(assemblyIdentity, out var existing)) {
            existing = new TemplateMetadata(_compilation, bytes);
            _templateMetadata.Add(assemblyIdentity, existing);
        }

        templateMetadata = existing;
        return true;
    }
}
