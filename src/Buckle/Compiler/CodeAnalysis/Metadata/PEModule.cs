using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TypeAttributes = System.Reflection.TypeAttributes;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule : IDisposable {
    private static readonly Dictionary<string, (int FirstIndex, int SecondIndex)> SharedEmptyForwardedTypes = [];
    private static readonly Dictionary<string, (string OriginalName, int FirstIndex, int SecondIndex)> SharedEmptyCaseInsensitiveForwardedTypes = [];

    private readonly ModuleMetadata _owner;
    private readonly PEReader _peReaderOpt;
    private readonly IntPtr _metadataPointerOpt;
    private readonly int _metadataSizeOpt;

    private MetadataReader _lazyMetadataReader;
    private ImmutableArray<AssemblyIdentity> _lazyAssemblyReferences;
    private Dictionary<string, (int FirstIndex, int SecondIndex)> _lazyForwardedTypesToAssemblyIndexMap;
    private Dictionary<string, (string OriginalName, int FirstIndex, int SecondIndex)> _lazyCaseInsensitiveForwardedTypesToAssemblyIndexMap;

    private readonly Lazy<IdentifierCollection> _lazyTypeNameCollection;
    private readonly Lazy<IdentifierCollection> _lazyNamespaceNameCollection;

    private string _lazyName;
    private bool _isDisposed;

    private ThreeState _lazyContainsNoPiaLocalTypes;
    private int[] _lazyNoPiaLocalTypeCheckBitMap;
    private ConcurrentDictionary<TypeDefinitionHandle, AttributeInfo> _lazyTypeDefToTypeIdentifierMap;

    private readonly CryptographicHashProvider _hashesOpt;

    private delegate bool AttributeValueExtractor<T>(out T value, ref BlobReader sigReader);
    private static readonly AttributeValueExtractor<string?> AttributeStringValueExtractor = CrackStringInAttributeValue;

    internal PEModule(
        ModuleMetadata owner,
        PEReader peReader,
        IntPtr metadataOpt,
        int metadataSizeOpt,
        bool includeEmbeddedInteropTypes,
        bool ignoreAssemblyRefs) {
        _owner = owner;
        _peReaderOpt = peReader;
        _metadataPointerOpt = metadataOpt;
        _metadataSizeOpt = metadataSizeOpt;
        _lazyTypeNameCollection = new Lazy<IdentifierCollection>(ComputeTypeNameCollection);
        _lazyNamespaceNameCollection = new Lazy<IdentifierCollection>(ComputeNamespaceNameCollection);
        _hashesOpt = (peReader is not null) ? new PEHashProvider(peReader) : null;
        _lazyContainsNoPiaLocalTypes = includeEmbeddedInteropTypes ? ThreeState.False : ThreeState.Unknown;

        if (ignoreAssemblyRefs)
            _lazyAssemblyReferences = [];
    }

    internal bool isManifestModule => metadataReader.IsAssembly;

    internal bool isLinkedModule => !metadataReader.IsAssembly;

    internal MetadataReader metadataReader {
        get {
            if (_lazyMetadataReader is null)
                InitializeMetadataReader();

            if (_isDisposed)
                ThrowMetadataDisposed();

            return _lazyMetadataReader;
        }
    }

    internal string name {
        get {
            _lazyName ??= metadataReader.GetString(metadataReader.GetModuleDefinition().Name);
            return _lazyName;
        }
    }

    internal ImmutableArray<AssemblyIdentity> referencedAssemblies {
        get {
            if (_lazyAssemblyReferences == null)
                _lazyAssemblyReferences = metadataReader.GetReferencedAssembliesOrThrow();

            return _lazyAssemblyReferences;
        }
    }

    internal bool bit32Required {
        get {
            if (_peReaderOpt is null)
                return false;

            return (_peReaderOpt.PEHeaders.CorHeader.Flags & CorFlags.Requires32Bit) != 0;
        }
    }

    internal bool isDisposed => _isDisposed;

    public void Dispose() {
        _isDisposed = true;
        _peReaderOpt?.Dispose();
    }

    internal Guid GetModuleVersionIdOrThrow() {
        return metadataReader.GetModuleVersionIdOrThrow();
    }

    private unsafe void InitializeMetadataReader() {
        MetadataReader newReader;

        if (_metadataPointerOpt != IntPtr.Zero) {
            newReader = new MetadataReader(
                (byte*)_metadataPointerOpt,
                _metadataSizeOpt,
                MetadataReaderOptions.ApplyWindowsRuntimeProjections,
                StringTableDecoder.Instance
            );
        } else {
            bool hasMetadata;

            try {
                hasMetadata = _peReaderOpt.HasMetadata;
            } catch {
                hasMetadata = false;
            }

            if (!hasMetadata) {
                // TODO Error
                // throw new BadImageFormatException(CodeAnalysisResources.PEImageDoesntContainManagedMetadata);
            }

            newReader = _peReaderOpt.GetMetadataReader(
                MetadataReaderOptions.ApplyWindowsRuntimeProjections,
                StringTableDecoder.Instance
            );
        }

        Interlocked.CompareExchange(ref _lazyMetadataReader, newReader, null);
    }

    internal ImmutableArray<string> GetMetadataModuleNamesOrThrow() {
        var builder = ArrayBuilder<string>.GetInstance();

        try {
            foreach (var fileHandle in metadataReader.AssemblyFiles) {
                var file = metadataReader.GetAssemblyFile(fileHandle);

                if (!file.ContainsMetadata)
                    continue;

                var moduleName = metadataReader.GetString(file.Name);

                // TODO Error
                // if (!MetadataHelpers.IsValidMetadataFileName(moduleName)) {
                //     throw new BadImageFormatException(string.Format(CodeAnalysisResources.InvalidModuleName, this.Name, moduleName));
                // }

                builder.Add(moduleName);
            }

            return builder.ToImmutable();
        } finally {
            builder.Free();
        }
    }

    private static void ThrowMetadataDisposed() {
        throw new ObjectDisposedException(nameof(ModuleMetadata));
    }

    private IdentifierCollection ComputeTypeNameCollection() {
        try {
            var allTypeDefs = GetTypeDefsOrThrow(topLevelOnly: false);
            var typeNames =
                from typeDef in allTypeDefs
                let metadataName = GetTypeDefNameOrThrow(typeDef.typeDef)
                let backtickIndex = metadataName.IndexOf('`')
                select backtickIndex < 0 ? metadataName : metadataName.Substring(0, backtickIndex);

            return new IdentifierCollection(typeNames);
        } catch (BadImageFormatException) {
            return new IdentifierCollection();
        }
    }

    private IdentifierCollection ComputeNamespaceNameCollection() {
        try {
            var allTypeIds = GetTypeDefsOrThrow(topLevelOnly: true);
            var fullNamespaceNames =
                from id in allTypeIds
                where !id.namespaceHandle.IsNil
                select metadataReader.GetString(id.namespaceHandle);

            var namespaceNames =
                from fullName in fullNamespaceNames.Distinct()
                from name in fullName.Split(['.'], StringSplitOptions.RemoveEmptyEntries)
                select name;

            return new IdentifierCollection(namespaceNames);
        } catch (BadImageFormatException) {
            return new IdentifierCollection();
        }
    }

    private IEnumerable<TypeDefToNamespace> GetTypeDefsOrThrow(bool topLevelOnly) {
        foreach (var typeDef in metadataReader.TypeDefinitions) {
            var row = metadataReader.GetTypeDefinition(typeDef);

            if (topLevelOnly && IsNested(row.Attributes))
                continue;

            yield return new TypeDefToNamespace(typeDef, row.NamespaceDefinition);
        }
    }

    internal static bool IsNested(TypeAttributes flags) {
        return (flags & ((TypeAttributes)0x00000006)) != 0;
    }

    internal string GetTypeDefNameOrThrow(TypeDefinitionHandle typeDef) {
        var typeDefinition = metadataReader.GetTypeDefinition(typeDef);
        var name = metadataReader.GetString(typeDefinition.Name);

        if (IsNestedTypeDefOrThrow(typeDef)) {
            var namespaceName = metadataReader.GetString(typeDefinition.Namespace);

            if (namespaceName.Length > 0)
                name = namespaceName + "." + name;
        }

        return name;
    }

    internal bool IsNestedTypeDefOrThrow(TypeDefinitionHandle typeDef) {
        return IsNestedTypeDefOrThrow(metadataReader, typeDef);
    }

    private static bool IsNestedTypeDefOrThrow(MetadataReader metadataReader, TypeDefinitionHandle typeDef) {
        return IsNested(metadataReader.GetTypeDefinition(typeDef).Attributes);
    }

    internal ImmutableArray<string> GetInternalsVisibleToAttributeValues(EntityHandle token) {
        var attrInfos = FindTargetAttributes(token, AttributeDescription.InternalsVisibleToAttribute);
        var result = ExtractStringValuesFromAttributes(attrInfos);
        return result?.ToImmutableAndFree() ?? [];
    }

    internal List<AttributeInfo> FindTargetAttributes(EntityHandle hasAttribute, AttributeDescription description) {
        List<AttributeInfo> result = null;

        try {
            foreach (var attributeHandle in metadataReader.GetCustomAttributes(hasAttribute)) {
                var signatureIndex = GetTargetAttributeSignatureIndex(attributeHandle, description);

                if (signatureIndex != -1) {
                    result ??= [];
                    result.Add(new AttributeInfo(attributeHandle, signatureIndex));
                }
            }
        } catch (BadImageFormatException) { }

        return result;
    }

    private ArrayBuilder<string> ExtractStringValuesFromAttributes(List<AttributeInfo> attrInfos) {
        if (attrInfos is null)
            return null;

        var result = ArrayBuilder<string>.GetInstance(attrInfos.Count);

        foreach (var ai in attrInfos) {
            if (TryExtractStringValueFromAttribute(ai.handle, out var extractedStr) && extractedStr is not null) {
                result.Add(extractedStr);
            }
        }

        return result;
    }

    internal bool TryExtractStringValueFromAttribute(CustomAttributeHandle handle, out string value) {
        return TryExtractValueFromAttribute(handle, out value, AttributeStringValueExtractor);
    }

    private bool TryExtractValueFromAttribute<T>(
        CustomAttributeHandle handle,
        out T value,
        AttributeValueExtractor<T> valueExtractor) {
        try {
            var valueBlob = GetCustomAttributeValueOrThrow(handle);

            if (!valueBlob.IsNil) {
                var reader = metadataReader.GetBlobReader(valueBlob);

                if (reader.Length > 4) {
                    if (reader.ReadByte() == 1 && reader.ReadByte() == 0)
                        return valueExtractor(out value, ref reader);
                }
            }
        } catch (BadImageFormatException) { }

        value = default;
        return false;
    }

    internal static bool CrackStringInAttributeValue(out string value, ref BlobReader sig) {
        try {
            if (sig.TryReadCompressedInteger(out var strLen) && sig.RemainingBytes >= strLen) {
                value = sig.ReadUTF8(strLen);
                value = value.TrimEnd('\0');
                return true;
            }

            value = null;

            return sig.RemainingBytes >= 1 && sig.ReadByte() == 0xFF;
        } catch (BadImageFormatException) {
            value = null;
            return false;
        }
    }

    internal BlobHandle GetCustomAttributeValueOrThrow(CustomAttributeHandle handle) {
        return metadataReader.GetCustomAttribute(handle).Value;
    }

    internal int GetTargetAttributeSignatureIndex(CustomAttributeHandle customAttribute, AttributeDescription description) {
        return GetTargetAttributeSignatureIndex(metadataReader, customAttribute, description, out _);
    }

    private static int GetTargetAttributeSignatureIndex(
        MetadataReader metadataReader,
        CustomAttributeHandle customAttribute,
        AttributeDescription description,
        out bool matchedAttributeType) {
        const int No = -1;

        if (!IsTargetAttribute(
            metadataReader,
            customAttribute,
            description.@namespace,
            description.name,
            out var ctor,
            description.matchIgnoringCase)) {
            matchedAttributeType = false;
            return No;
        }

        matchedAttributeType = true;

        try {
            var sig = metadataReader.GetBlobReader(GetMethodSignatureOrThrow(metadataReader, ctor));

            for (var i = 0; i < description.signatures.Length; i++) {
                var targetSignature = description.signatures[i];
                sig.Reset();

                if (sig.RemainingBytes >= 3 &&
                    sig.ReadByte() == targetSignature[0] &&
                    sig.ReadByte() == targetSignature[1] &&
                    sig.ReadByte() == targetSignature[2]) {
                    var j = 3;

                    for (; j < targetSignature.Length; j++) {
                        if (sig.RemainingBytes == 0)
                            break;

                        var b = sig.ReadSignatureTypeCode();

                        if ((SignatureTypeCode)targetSignature[j] == b) {
                            switch (b) {
                                case SignatureTypeCode.TypeHandle:
                                    var token = sig.ReadTypeHandle();
                                    var tokenType = token.Kind;
                                    StringHandle name;
                                    StringHandle ns;

                                    if (tokenType == HandleKind.TypeDefinition) {
                                        var typeHandle = (TypeDefinitionHandle)token;

                                        if (IsNestedTypeDefOrThrow(metadataReader, typeHandle))
                                            break;

                                        var typeDef = metadataReader.GetTypeDefinition(typeHandle);
                                        name = typeDef.Name;
                                        ns = typeDef.Namespace;
                                    } else if (tokenType == HandleKind.TypeReference) {
                                        var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)token);

                                        if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
                                            break;

                                        name = typeRef.Name;
                                        ns = typeRef.Namespace;
                                    } else {
                                        break;
                                    }

                                    var targetInfo = AttributeDescription.TypeHandleTargets[targetSignature[j + 1]];

                                    if (StringEquals(metadataReader, ns, targetInfo.@namespace, ignoreCase: false) &&
                                        StringEquals(metadataReader, name, targetInfo.name, ignoreCase: false)) {
                                        j++;
                                        continue;
                                    }

                                    break;
                                case SignatureTypeCode.SZArray:
                                    continue;
                                default:
                                    continue;
                            }
                        }

                        break;
                    }

                    if (sig.RemainingBytes == 0 && j == targetSignature.Length)
                        return i;
                }
            }
        } catch (BadImageFormatException) { }

        return No;
    }

    internal bool IsTargetAttribute(
        CustomAttributeHandle customAttribute,
        string namespaceName,
        string typeName,
        out EntityHandle ctor,
        bool ignoreCase = false) {
        return IsTargetAttribute(metadataReader, customAttribute, namespaceName, typeName, out ctor, ignoreCase);
    }

    private static bool IsTargetAttribute(
        MetadataReader metadataReader,
        CustomAttributeHandle customAttribute,
        string namespaceName,
        string typeName,
        out EntityHandle ctor,
        bool ignoreCase) {
        if (!GetTypeAndConstructor(metadataReader, customAttribute, out var ctorType, out ctor))
            return false;

        if (!GetAttributeNamespaceAndName(metadataReader, ctorType, out var ctorTypeNamespace, out var ctorTypeName))
            return false;

        try {
            return StringEquals(metadataReader, ctorTypeName, typeName, ignoreCase)
                && StringEquals(metadataReader, ctorTypeNamespace, namespaceName, ignoreCase);
        } catch (BadImageFormatException) {
            return false;
        }
    }

    private static bool GetTypeAndConstructor(
        MetadataReader metadataReader,
        CustomAttributeHandle customAttribute,
        out EntityHandle ctorType,
        out EntityHandle attributeCtor) {
        try {
            ctorType = default;
            attributeCtor = metadataReader.GetCustomAttribute(customAttribute).Constructor;

            if (attributeCtor.Kind == HandleKind.MemberReference) {
                var memberRef = metadataReader.GetMemberReference((MemberReferenceHandle)attributeCtor);
                var ctorName = memberRef.Name;

                if (!metadataReader.StringComparer.Equals(ctorName, WellKnownMemberNames.InstanceConstructorName))
                    return false;

                ctorType = memberRef.Parent;
            } else if (attributeCtor.Kind == HandleKind.MethodDefinition) {
                var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor);

                if (!metadataReader.StringComparer.Equals(methodDef.Name, WellKnownMemberNames.InstanceConstructorName))
                    return false;

                ctorType = methodDef.GetDeclaringType();
            } else {
                return false;
            }

            return true;
        } catch (BadImageFormatException) {
            ctorType = default;
            attributeCtor = default;
            return false;
        }
    }

    private static bool GetAttributeNamespaceAndName(
        MetadataReader metadataReader,
        EntityHandle typeDefOrRef,
        out StringHandle namespaceHandle,
        out StringHandle nameHandle) {
        nameHandle = default;
        namespaceHandle = default;

        try {
            if (typeDefOrRef.Kind == HandleKind.TypeReference) {
                var typeRefRow = metadataReader.GetTypeReference((TypeReferenceHandle)typeDefOrRef);
                var handleType = typeRefRow.ResolutionScope.Kind;

                if (handleType == HandleKind.TypeReference || handleType == HandleKind.TypeDefinition)
                    return false;

                nameHandle = typeRefRow.Name;
                namespaceHandle = typeRefRow.Namespace;
            } else if (typeDefOrRef.Kind == HandleKind.TypeDefinition) {
                var def = metadataReader.GetTypeDefinition((TypeDefinitionHandle)typeDefOrRef);

                if (IsNested(def.Attributes))
                    return false;

                nameHandle = def.Name;
                namespaceHandle = def.Namespace;
            } else {
                return false;
            }

            return true;
        } catch (BadImageFormatException) {
            return false;
        }
    }

    private static bool StringEquals(
        MetadataReader metadataReader,
        StringHandle nameHandle,
        string name,
        bool ignoreCase) {
        if (ignoreCase)
            return string.Equals(metadataReader.GetString(nameHandle), name, StringComparison.OrdinalIgnoreCase);

        return metadataReader.StringComparer.Equals(nameHandle, name);
    }

    private static BlobHandle GetMethodSignatureOrThrow(MetadataReader metadataReader, EntityHandle methodDefOrRef) {
        return methodDefOrRef.Kind switch {
            HandleKind.MethodDefinition => GetMethodSignatureOrThrow(metadataReader, (MethodDefinitionHandle)methodDefOrRef),
            HandleKind.MemberReference => (BlobHandle)GetSignatureOrThrow(metadataReader, (MemberReferenceHandle)methodDefOrRef),
            _ => throw ExceptionUtilities.UnexpectedValue(methodDefOrRef.Kind),
        };
    }

    private static BlobHandle GetSignatureOrThrow(MetadataReader metadataReader, MemberReferenceHandle memberRef) {
        return metadataReader.GetMemberReference(memberRef).Signature;
    }

    internal bool ContainsNoPiaLocalTypes() {
        if (_lazyContainsNoPiaLocalTypes == ThreeState.Unknown) {
            try {
                foreach (var attributeHandle in metadataReader.CustomAttributes) {
                    var signatureIndex = IsTypeIdentifierAttribute(attributeHandle);

                    if (signatureIndex != -1) {
                        _lazyContainsNoPiaLocalTypes = ThreeState.True;
                        var parent = (TypeDefinitionHandle)metadataReader.GetCustomAttribute(attributeHandle).Parent;
                        RegisterNoPiaLocalType(parent, attributeHandle, signatureIndex);
                        return true;
                    }
                }
            } catch (BadImageFormatException) { }

            _lazyContainsNoPiaLocalTypes = ThreeState.False;
        }

        return _lazyContainsNoPiaLocalTypes == ThreeState.True;
    }

    private int IsTypeIdentifierAttribute(CustomAttributeHandle customAttribute) {
        const int No = -1;

        try {
            if (metadataReader.GetCustomAttribute(customAttribute).Parent.Kind != HandleKind.TypeDefinition)
                return No;

            return GetTargetAttributeSignatureIndex(customAttribute, AttributeDescription.TypeIdentifierAttribute);
        } catch (BadImageFormatException) {
            return No;
        }
    }

    private void RegisterNoPiaLocalType(
        TypeDefinitionHandle typeDef,
        CustomAttributeHandle customAttribute,
        int signatureIndex) {
        if (_lazyNoPiaLocalTypeCheckBitMap is null) {
            Interlocked.CompareExchange(
                ref _lazyNoPiaLocalTypeCheckBitMap,
                new int[(metadataReader.TypeDefinitions.Count + 32) / 32],
                null);
        }

        if (_lazyTypeDefToTypeIdentifierMap is null) {
            Interlocked.CompareExchange(
                ref _lazyTypeDefToTypeIdentifierMap,
                new ConcurrentDictionary<TypeDefinitionHandle, AttributeInfo>(),
                null);
        }

        _lazyTypeDefToTypeIdentifierMap.TryAdd(typeDef, new AttributeInfo(customAttribute, signatureIndex));

        RecordNoPiaLocalTypeCheck(typeDef);
    }

    private void RecordNoPiaLocalTypeCheck(TypeDefinitionHandle typeDef) {
        if (_lazyNoPiaLocalTypeCheckBitMap is null)
            return;

        var rid = MetadataTokens.GetRowNumber(typeDef);
        var item = rid / 32;
        var bit = 1 << (rid % 32);
        int oldValue;

        do {
            oldValue = _lazyNoPiaLocalTypeCheckBitMap[item];
        } while (Interlocked.CompareExchange(
                 ref _lazyNoPiaLocalTypeCheckBitMap[item],
                 oldValue | bit,
                 oldValue) != oldValue);
    }

    internal AssemblyIdentity ReadAssemblyIdentityOrThrow() {
        return metadataReader.ReadAssemblyIdentityOrThrow();
    }
}
