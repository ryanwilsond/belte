using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TypeAttributes = System.Reflection.TypeAttributes;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule : IDisposable {
    private static readonly Dictionary<string, (int FirstIndex, int SecondIndex)> SharedEmptyForwardedTypes = [];
    private static readonly Dictionary<string, (string OriginalName, int FirstIndex, int SecondIndex)> SharedEmptyCaseInsensitiveForwardedTypes = [];

    private const string VTableGapMethodNamePrefix = "_VtblGap";

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
    private static readonly AttributeValueExtractor<string> AttributeStringValueExtractor = CrackStringInAttributeValue;
    private static readonly AttributeValueExtractor<int> AttributeIntValueExtractor = CrackIntInAttributeValue;
    private static readonly AttributeValueExtractor<bool> AttributeBooleanValueExtractor = CrackBooleanInAttributeValue;
    private static readonly AttributeValueExtractor<StringAndInt> AttributeStringAndIntValueExtractor = CrackStringAndIntInAttributeValue;
    private static readonly AttributeValueExtractor<byte> AttributeByteValueExtractor = CrackByteInAttributeValue;
    private static readonly AttributeValueExtractor<ImmutableArray<byte>> AttributeByteArrayValueExtractor = CrackByteArrayInAttributeValue;

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

    internal IdentifierCollection typeNames => _lazyTypeNameCollection.Value;

    internal IdentifierCollection namespaceNames => _lazyNamespaceNameCollection.Value;

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

    internal bool IsNoPiaLocalType(TypeDefinitionHandle typeDef) {
        return IsNoPiaLocalType(typeDef, out _);
    }

    internal IEnumerable<IGrouping<string, TypeDefinitionHandle>> GroupTypesByNamespaceOrThrow(
        StringComparer nameComparer) {
        var namespaces = new Dictionary<string, ArrayBuilder<TypeDefinitionHandle>>();

        GetTypeNamespaceNamesOrThrow(namespaces);
        GetForwardedTypeNamespaceNamesOrThrow(namespaces);

        var result = new ArrayBuilder<IGrouping<string, TypeDefinitionHandle>>(namespaces.Count);

        foreach (var pair in namespaces) {
            result.Add(new Grouping<string, TypeDefinitionHandle>(
                pair.Key,
                pair.Value ?? SpecializedCollections.EmptyEnumerable<TypeDefinitionHandle>()
            ));
        }

        result.Sort(new TypesByNamespaceSortComparer(nameComparer));
        return result;
    }

    internal IEnumerable<KeyValuePair<string, (int FirstIndex, int SecondIndex)>> GetForwardedTypes() {
        EnsureForwardTypeToAssemblyMap();
        return _lazyForwardedTypesToAssemblyIndexMap;
    }

    private void GetTypeNamespaceNamesOrThrow(Dictionary<string, ArrayBuilder<TypeDefinitionHandle>> namespaces) {
        var namespaceHandles = new Dictionary<NamespaceDefinitionHandle, ArrayBuilder<TypeDefinitionHandle>>(
            NamespaceHandleEqualityComparer.Singleton
        );

        foreach (var pair in GetTypeDefsOrThrow(topLevelOnly: true)) {
            var nsHandle = pair.namespaceHandle;
            var typeDef = pair.typeDef;

            if (namespaceHandles.TryGetValue(nsHandle, out var builder))
                builder.Add(typeDef);
            else
                namespaceHandles.Add(nsHandle, [typeDef]);
        }

        foreach (var kvp in namespaceHandles) {
            var @namespace = metadataReader.GetString(kvp.Key);

            if (namespaces.TryGetValue(@namespace, out var builder))
                builder.AddRange(kvp.Value);
            else
                namespaces.Add(@namespace, kvp.Value);
        }
    }

    private void GetForwardedTypeNamespaceNamesOrThrow(
        Dictionary<string, ArrayBuilder<TypeDefinitionHandle>> namespaces) {
        EnsureForwardTypeToAssemblyMap();

        foreach (var typeName in _lazyForwardedTypesToAssemblyIndexMap.Keys) {
            var index = typeName.LastIndexOf('.');
            var namespaceName = index >= 0 ? typeName.Substring(0, index) : "";

            if (!namespaces.ContainsKey(namespaceName))
                namespaces.Add(namespaceName, null);
        }
    }

    private void EnsureForwardTypeToAssemblyMap() {
        if (_lazyForwardedTypesToAssemblyIndexMap is null) {
            Dictionary<string, (int FirstIndex, int SecondIndex)>? typesToAssemblyIndexMap = null;

            try {
                var forwarders = metadataReader.ExportedTypes;

                foreach (var handle in forwarders) {
                    var exportedType = metadataReader.GetExportedType(handle);

                    if (!exportedType.IsForwarder)
                        continue;

                    var refHandle = (AssemblyReferenceHandle)exportedType.Implementation;

                    if (refHandle.IsNil)
                        continue;

                    int referencedAssemblyIndex;

                    try {
                        referencedAssemblyIndex = GetAssemblyReferenceIndexOrThrow(refHandle);
                    } catch (BadImageFormatException) {
                        continue;
                    }

                    if (referencedAssemblyIndex < 0 || referencedAssemblyIndex >= referencedAssemblies.Length)
                        continue;

                    var name = metadataReader.GetString(exportedType.Name);
                    var ns = exportedType.Namespace;

                    if (!ns.IsNil) {
                        var namespaceString = metadataReader.GetString(ns);

                        if (namespaceString.Length > 0)
                            name = namespaceString + "." + name;
                    }

                    typesToAssemblyIndexMap ??= [];

                    if (typesToAssemblyIndexMap.TryGetValue(name, out var indices)) {
                        if (indices.FirstIndex != referencedAssemblyIndex && indices.SecondIndex < 0) {
                            indices.SecondIndex = referencedAssemblyIndex;
                            typesToAssemblyIndexMap[name] = indices;
                        }
                    } else {
                        typesToAssemblyIndexMap.Add(name, (FirstIndex: referencedAssemblyIndex, SecondIndex: -1));
                    }
                }
            } catch (BadImageFormatException) { }

            if (typesToAssemblyIndexMap is null)
                _lazyForwardedTypesToAssemblyIndexMap = SharedEmptyForwardedTypes;
            else
                _lazyForwardedTypesToAssemblyIndexMap = typesToAssemblyIndexMap;
        }
    }

    internal int GetAssemblyReferenceIndexOrThrow(AssemblyReferenceHandle assemblyRef) {
        return metadataReader.GetRowNumber(assemblyRef) - 1;
    }

    internal bool HasStringValuedAttribute(EntityHandle token, AttributeDescription description, out string value) {
        var info = FindTargetAttribute(token, description);

        if (info.hasValue)
            return TryExtractStringValueFromAttribute(info.handle, out value);

        value = null;
        return false;
    }

    internal AttributeInfo FindTargetAttribute(EntityHandle hasAttribute, AttributeDescription description) {
        return FindTargetAttribute(metadataReader, hasAttribute, description, out _);
    }

    internal static AttributeInfo FindTargetAttribute(
        MetadataReader metadataReader,
        EntityHandle hasAttribute,
        AttributeDescription description,
        out bool foundAttributeType) {
        foundAttributeType = false;

        try {
            foreach (var attributeHandle in metadataReader.GetCustomAttributes(hasAttribute)) {
                var signatureIndex = GetTargetAttributeSignatureIndex(
                    metadataReader,
                    attributeHandle,
                    description,
                    out var matchedAttributeType
                );

                if (matchedAttributeType)
                    foundAttributeType = true;

                if (signatureIndex != -1)
                    return new AttributeInfo(attributeHandle, signatureIndex);
            }
        } catch (BadImageFormatException) { }

        return default;
    }

    internal CustomAttributeHandleCollection GetCustomAttributesOrThrow(EntityHandle handle) {
        return metadataReader.GetCustomAttributes(handle);
    }

    private bool IsNoPiaLocalType(TypeDefinitionHandle typeDef, out AttributeInfo attributeInfo) {
        if (_lazyContainsNoPiaLocalTypes == ThreeState.False) {
            attributeInfo = default;
            return false;
        }

        if (_lazyNoPiaLocalTypeCheckBitMap is not null && _lazyTypeDefToTypeIdentifierMap is not null) {
            var rid = metadataReader.GetRowNumber(typeDef);
            var item = rid / 32;
            var bit = 1 << (rid % 32);

            if ((_lazyNoPiaLocalTypeCheckBitMap[item] & bit) != 0)
                return _lazyTypeDefToTypeIdentifierMap.TryGetValue(typeDef, out attributeInfo);
        }

        try {
            foreach (var attributeHandle in metadataReader.GetCustomAttributes(typeDef)) {
                var signatureIndex = IsTypeIdentifierAttribute(attributeHandle);

                if (signatureIndex != -1) {
                    _lazyContainsNoPiaLocalTypes = ThreeState.True;
                    RegisterNoPiaLocalType(typeDef, attributeHandle, signatureIndex);
                    attributeInfo = new AttributeInfo(attributeHandle, signatureIndex);
                    return true;
                }
            }
        } catch (BadImageFormatException) { }

        RecordNoPiaLocalTypeCheck(typeDef);
        attributeInfo = default;
        return false;
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

            if (!hasMetadata)
                throw new BadImageFormatException();

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

    internal ImmutableArray<byte> GetHash(AssemblyHashAlgorithm algorithmId) {
        return _hashesOpt.GetHash(algorithmId);
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

    private static bool CrackIntInAttributeValue(out int value, ref BlobReader sig) {
        if (sig.RemainingBytes >= 4) {
            value = sig.ReadInt32();
            return true;
        }

        value = -1;
        return false;
    }

    private static bool CrackBooleanInAttributeValue(out bool value, ref BlobReader sig) {
        if (sig.RemainingBytes >= 1) {
            value = sig.ReadBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool CrackStringAndIntInAttributeValue(out StringAndInt value, ref BlobReader sig) {
        value = default;
        return CrackStringInAttributeValue(out value.stringValue, ref sig) &&
            CrackIntInAttributeValue(out value.intValue, ref sig);
    }

    private static bool CrackByteInAttributeValue(out byte value, ref BlobReader sig) {
        if (sig.RemainingBytes >= 1) {
            value = sig.ReadByte();
            return true;
        }

        value = 0xff;
        return false;
    }

    private static bool CrackByteArrayInAttributeValue(out ImmutableArray<byte> value, ref BlobReader sig) {
        if (sig.RemainingBytes >= 4) {
            var arrayLen = sig.ReadUInt32();

            if (IsArrayNull(arrayLen)) {
                value = default;
                return false;
            }

            if (sig.RemainingBytes >= arrayLen) {
                var byteArrayBuilder = ArrayBuilder<byte>.GetInstance((int)arrayLen);

                for (var i = 0; i < arrayLen; i++)
                    byteArrayBuilder.Add(sig.ReadByte());

                value = byteArrayBuilder.ToImmutableAndFree();
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsArrayNull(uint length) {
        const uint NullArray = 0xFFFF_FFFF;

        if (length == NullArray)
            return true;

        return false;
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

    private static BlobHandle GetMethodSignatureOrThrow(MetadataReader metadataReader, MethodDefinitionHandle methodDef) {
        return metadataReader.GetMethodDefinition(methodDef).Signature;
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

    internal bool HasRefSafetyRulesAttribute(EntityHandle token, out int version, out bool foundAttributeType) {
        var info = FindTargetAttribute(metadataReader, token, AttributeDescription.RefSafetyRulesAttribute, out foundAttributeType);

        if (info.hasValue) {
            if (TryExtractValueFromAttribute(info.handle, out var value, AttributeIntValueExtractor)) {
                version = value;
                return true;
            }
        }

        version = 0;
        return false;
    }

    internal (int FirstIndex, int SecondIndex) GetAssemblyRefsForForwardedType(
        string fullName,
        bool ignoreCase,
        out string matchedName) {
        EnsureForwardTypeToAssemblyMap();

        if (ignoreCase) {
            EnsureCaseInsensitiveDictionary();

            if (_lazyCaseInsensitiveForwardedTypesToAssemblyIndexMap.TryGetValue(fullName, out var value)) {
                matchedName = value.OriginalName;
                return (value.FirstIndex, value.SecondIndex);
            }
        } else {
            if (_lazyForwardedTypesToAssemblyIndexMap.TryGetValue(fullName, out var assemblyIndices)) {
                matchedName = fullName;
                return assemblyIndices;
            }
        }

        matchedName = null;
        return (FirstIndex: -1, SecondIndex: -1);

        void EnsureCaseInsensitiveDictionary() {
            if (_lazyCaseInsensitiveForwardedTypesToAssemblyIndexMap is not null)
                return;

            if (_lazyForwardedTypesToAssemblyIndexMap.Count == 0) {
                _lazyCaseInsensitiveForwardedTypesToAssemblyIndexMap = SharedEmptyCaseInsensitiveForwardedTypes;
                return;
            }

            var caseInsensitiveMap = new Dictionary<string, (string OriginalName, int FirstIndex, int SecondIndex)>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var (key, (firstIndex, secondIndex)) in _lazyForwardedTypesToAssemblyIndexMap)
                _ = caseInsensitiveMap.TryAdd(key, (key, firstIndex, secondIndex));

            _lazyCaseInsensitiveForwardedTypesToAssemblyIndexMap = caseInsensitiveMap;
        }
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


    internal bool HasNullablePublicOnlyAttribute(EntityHandle token, out bool includesInternals) {
        var info = FindTargetAttribute(token, AttributeDescription.NullablePublicOnlyAttribute);

        if (info.hasValue) {
            if (TryExtractValueFromAttribute(info.handle, out var value, AttributeBooleanValueExtractor)) {
                includesInternals = value;
                return true;
            }
        }

        includesInternals = false;
        return false;
    }

    internal bool HasGuidAttribute(EntityHandle token, out string guidValue) {
        return HasStringValuedAttribute(token, AttributeDescription.GuidAttribute, out guidValue);
    }

    internal ModuleMetadata GetNonDisposableMetadata() => _owner.Copy();

    internal void GetFieldDefPropsOrThrow(
        FieldDefinitionHandle fieldDef,
        out string name,
        out FieldAttributes flags) {
        var fieldRow = metadataReader.GetFieldDefinition(fieldDef);
        name = metadataReader.GetString(fieldRow.Name);
        flags = fieldRow.Attributes;
    }

    internal bool HasFixedBufferAttribute(EntityHandle token, out string elementTypeName, out int bufferSize) {
        return HasStringAndIntValuedAttribute(token, AttributeDescription.FixedBufferAttribute, out elementTypeName, out bufferSize);
    }

    private bool HasStringAndIntValuedAttribute(EntityHandle token, AttributeDescription description, out string stringValue, out int intValue) {
        var info = FindTargetAttribute(token, description);

        if (info.hasValue)
            return TryExtractStringAndIntValueFromAttribute(info.handle, out stringValue, out intValue);

        stringValue = null;
        intValue = 0;
        return false;
    }

    private bool TryExtractStringAndIntValueFromAttribute(
        CustomAttributeHandle handle,
        out string stringValue,
        out int intValue) {
        var result = TryExtractValueFromAttribute(handle, out var data, AttributeStringAndIntValueExtractor);
        stringValue = data.stringValue;
        intValue = data.intValue;
        return result;
    }

    internal GenericParameterHandleCollection GetTypeDefGenericParamsOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetGenericParameters();
    }

    internal ConstantValue GetConstantFieldValue(FieldDefinitionHandle fieldDef) {
        try {
            var constantHandle = metadataReader.GetFieldDefinition(fieldDef).GetDefaultValue();

            return constantHandle.IsNil ? null : GetConstantValueOrThrow(constantHandle);
        } catch (BadImageFormatException) {
            return null;
        }
    }

    private ConstantValue GetConstantValueOrThrow(ConstantHandle handle) {
        var constantRow = metadataReader.GetConstant(handle);
        var reader = metadataReader.GetBlobReader(constantRow.Value);

        switch (constantRow.TypeCode) {
            case ConstantTypeCode.Boolean:
                return new ConstantValue(reader.ReadBoolean(), SpecialType.Bool);
            case ConstantTypeCode.Char:
                return new ConstantValue(reader.ReadChar(), SpecialType.Char);
            case ConstantTypeCode.SByte:
                return new ConstantValue(reader.ReadSByte(), SpecialType.Int8);
            case ConstantTypeCode.Int16:
                return new ConstantValue(reader.ReadInt16(), SpecialType.Int16);
            case ConstantTypeCode.Int32:
                return new ConstantValue(reader.ReadInt32(), SpecialType.Int32);
            case ConstantTypeCode.Int64:
                return new ConstantValue(reader.ReadInt64(), SpecialType.Int64);
            case ConstantTypeCode.Byte:
                return new ConstantValue(reader.ReadByte(), SpecialType.UInt8);
            case ConstantTypeCode.UInt16:
                return new ConstantValue(reader.ReadUInt16(), SpecialType.UInt16);
            case ConstantTypeCode.UInt32:
                return new ConstantValue(reader.ReadUInt32(), SpecialType.UInt32);
            case ConstantTypeCode.UInt64:
                return new ConstantValue(reader.ReadUInt64(), SpecialType.UInt64);
            case ConstantTypeCode.Single:
                return new ConstantValue(reader.ReadSingle(), SpecialType.Float32);
            case ConstantTypeCode.Double:
                return new ConstantValue(reader.ReadDouble(), SpecialType.Float64);
            case ConstantTypeCode.String:
                return new ConstantValue(reader.ReadUTF16(reader.Length), SpecialType.String);
            case ConstantTypeCode.NullReference:
                if (reader.ReadUInt32() == 0) {
                    // TODO Correct equivalency?
                    return ConstantValue.Null;
                }

                break;
        }

        return null;
    }

    internal EntityHandle GetBaseTypeOfTypeOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).BaseType;
    }

    internal TypeAttributes GetTypeDefFlagsOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).Attributes;
    }

    internal void GetGenericParamPropsOrThrow(
        GenericParameterHandle handle,
        out string name,
        out GenericParameterAttributes flags) {
        var row = metadataReader.GetGenericParameter(handle);
        name = metadataReader.GetString(row.Name);
        flags = row.Attributes;
    }

    internal ImmutableArray<TypeDefinitionHandle> GetNestedTypeDefsOrThrow(TypeDefinitionHandle container) {
        return metadataReader.GetTypeDefinition(container).GetNestedTypes();
    }

    internal FieldAttributes GetFieldDefFlagsOrThrow(FieldDefinitionHandle fieldDef) {
        return metadataReader.GetFieldDefinition(fieldDef).Attributes;
    }

    internal FieldDefinitionHandleCollection GetFieldsOfTypeOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetFields();
    }

    internal MethodDefinitionHandleCollection GetMethodsOfTypeOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetMethods();
    }

    internal MethodAttributes GetMethodDefFlagsOrThrow(MethodDefinitionHandle methodDef) {
        return metadataReader.GetMethodDefinition(methodDef).Attributes;
    }

    internal string GetMethodDefNameOrThrow(MethodDefinitionHandle methodDef) {
        return metadataReader.GetString(metadataReader.GetMethodDefinition(methodDef).Name);
    }

    internal MethodImplementationHandleCollection GetMethodImplementationsOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetMethodImplementations();
    }

    internal string GetFieldDefNameOrThrow(FieldDefinitionHandle fieldDef) {
        return metadataReader.GetString(metadataReader.GetFieldDefinition(fieldDef).Name);
    }

    internal void GetMethodDefPropsOrThrow(
        MethodDefinitionHandle methodDef,
        out string name,
        out System.Reflection.MethodImplAttributes implFlags,
        out MethodAttributes flags,
        out int rva) {
        var methodRow = metadataReader.GetMethodDefinition(methodDef);
        name = metadataReader.GetString(methodRow.Name);
        implFlags = methodRow.ImplAttributes;
        flags = methodRow.Attributes;
        rva = methodRow.RelativeVirtualAddress;
    }

    internal bool HasIsByRefLikeAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.IsByRefLikeAttribute).hasValue;
    }

    internal void GetMethodImplPropsOrThrow(
        MethodImplementationHandle methodImpl,
        out EntityHandle body,
        out EntityHandle declaration) {
        var impl = metadataReader.GetMethodImplementation(methodImpl);
        body = impl.MethodBody;
        declaration = impl.MethodDeclaration;
    }

    internal bool ShouldImportField(FieldDefinitionHandle field, MetadataImportOptions importOptions) {
        try {
            var flags = GetFieldDefFlagsOrThrow(field);
            return ShouldImportField(flags, importOptions);
        } catch (BadImageFormatException) {
            return true;
        }
    }

    internal static bool ShouldImportField(FieldAttributes flags, MetadataImportOptions importOptions) {
        switch (flags & FieldAttributes.FieldAccessMask) {
            case FieldAttributes.Private:
            case FieldAttributes.PrivateScope:
                return importOptions == MetadataImportOptions.All;
            case FieldAttributes.Assembly:
                return importOptions >= MetadataImportOptions.Internal;
            default:
                return true;
        }
    }

    internal bool ShouldImportMethod(
        TypeDefinitionHandle typeDef,
        MethodDefinitionHandle methodDef,
        MetadataImportOptions importOptions) {
        try {
            var flags = GetMethodDefFlagsOrThrow(methodDef);

            if ((flags & MethodAttributes.Virtual) == 0 && !AcceptBasedOnAccessibility(importOptions, flags) &&
                ((flags & MethodAttributes.Static) == 0 || !IsMethodImpl(typeDef, methodDef))) {
                return false;
            }
        } catch (BadImageFormatException) { }

        try {
            var name = GetMethodDefNameOrThrow(methodDef);
            return !name.StartsWith(VTableGapMethodNamePrefix, StringComparison.Ordinal);
        } catch (BadImageFormatException) {
            return true;
        }

        static bool AcceptBasedOnAccessibility(MetadataImportOptions importOptions, MethodAttributes flags) {
            switch (flags & MethodAttributes.MemberAccessMask) {
                case MethodAttributes.Private:
                case MethodAttributes.PrivateScope:
                    if (importOptions != MetadataImportOptions.All) {
                        return false;
                    }

                    break;

                case MethodAttributes.Assembly:
                    if (importOptions == MetadataImportOptions.Public) {
                        return false;
                    }

                    break;
            }

            return true;
        }

        bool IsMethodImpl(TypeDefinitionHandle typeDef, MethodDefinitionHandle methodDef) {
            foreach (var methodImpl in GetMethodImplementationsOrThrow(typeDef)) {
                GetMethodImplPropsOrThrow(methodImpl, out var body, out _);

                if (body == methodDef)
                    return true;
            }

            return false;
        }
    }

    internal bool HasIsUnmanagedAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.IsUnmanagedAttribute).hasValue;
    }

    internal bool HasNullableAttribute(EntityHandle token, out byte defaultTransform, out ImmutableArray<byte> nullableTransforms) {
        var info = FindTargetAttribute(token, AttributeDescription.NullableAttribute);

        defaultTransform = 0;
        nullableTransforms = default;

        if (!info.hasValue)
            return false;

        if (info.signatureIndex == 0)
            return TryExtractValueFromAttribute(info.handle, out defaultTransform, AttributeByteValueExtractor);

        return TryExtractByteArrayValueFromAttribute(info.handle, out nullableTransforms);
    }

    private bool TryExtractByteArrayValueFromAttribute(CustomAttributeHandle handle, out ImmutableArray<byte> value) {
        return TryExtractValueFromAttribute(handle, out value, AttributeByteArrayValueExtractor);
    }

    internal bool HasNullableContextAttribute(EntityHandle token, out byte value) {
        var info = FindTargetAttribute(token, AttributeDescription.NullableContextAttribute);

        if (!info.hasValue) {
            value = 0;
            return false;
        }

        return TryExtractValueFromAttribute(info.handle, out value, AttributeByteValueExtractor);
    }

    internal bool HasIsReadOnlyAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.IsReadOnlyAttribute).hasValue;
    }

    internal BlobHandle GetMethodSignatureOrThrow(MethodDefinitionHandle methodDef) {
        return GetMethodSignatureOrThrow(metadataReader, methodDef);
    }

    internal bool HasUnscopedRefAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.UnscopedRefAttribute).hasValue;
    }

    internal GenericParameterHandleCollection GetGenericParametersForMethodOrThrow(MethodDefinitionHandle methodDef) {
        return metadataReader.GetMethodDefinition(methodDef).GetGenericParameters();
    }

    internal TypeDefinitionHandle GetContainingTypeOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetDeclaringType();
    }

    internal string GetTypeDefNamespaceOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetString(metadataReader.GetTypeDefinition(typeDef).Namespace);
    }

    internal bool HasGenericParametersOrThrow(TypeDefinitionHandle typeDef) {
        return metadataReader.GetTypeDefinition(typeDef).GetGenericParameters().Count > 0;
    }

    internal bool IsInterfaceOrThrow(TypeDefinitionHandle typeDef) {
        // TODO interfaces
        // return metadataReader.GetTypeDefinition(typeDef).Attributes.IsInterface();
        return false;
    }

    internal bool IsNoPiaLocalType(
        TypeDefinitionHandle typeDef,
        out string interfaceGuid,
        out string scope,
        out string identifier) {
        if (!IsNoPiaLocalType(typeDef, out var typeIdentifierInfo)) {
            interfaceGuid = null;
            scope = null;
            identifier = null;

            return false;
        }

        interfaceGuid = null;
        scope = null;
        identifier = null;

        try {
            // if (GetTypeDefFlagsOrThrow(typeDef).IsInterface()) {
            //     HasGuidAttribute(typeDef, out interfaceGuid);
            // }

            if (typeIdentifierInfo.signatureIndex == 1) {
                var valueBlob = GetCustomAttributeValueOrThrow(typeIdentifierInfo.handle);

                if (!valueBlob.IsNil) {
                    var reader = metadataReader.GetBlobReader(valueBlob);

                    if (reader.Length > 4) {
                        if (reader.ReadInt16() == 1) {
                            if (!CrackStringInAttributeValue(out scope, ref reader) ||
                                !CrackStringInAttributeValue(out identifier, ref reader)) {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        } catch (BadImageFormatException) {
            return false;
        }
    }

    internal void GetParamPropsOrThrow(
        ParameterHandle parameterDef,
        out string name,
        out ParameterAttributes flags) {
        var parameter = metadataReader.GetParameter(parameterDef);
        name = metadataReader.GetString(parameter.Name);
        flags = parameter.Attributes;
    }

    internal bool HasRequiresLocationAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.RequiresLocationAttribute).hasValue;
    }

    internal bool HasScopedRefAttribute(EntityHandle token) {
        return FindTargetAttribute(token, AttributeDescription.ScopedRefAttribute).hasValue;
    }

    internal ConstantValue GetParamDefaultValue(ParameterHandle param) {
        try {
            var constantHandle = metadataReader.GetParameter(param).GetDefaultValue();
            return constantHandle.IsNil ? null : GetConstantValueOrThrow(constantHandle);
        } catch (BadImageFormatException) {
            return null;
        }
    }

    internal BlobReader GetMemoryReaderOrThrow(BlobHandle blob) {
        return metadataReader.GetBlobReader(blob);
    }

    internal BlobHandle GetFieldSignatureOrThrow(FieldDefinitionHandle fieldDef) {
        return metadataReader.GetFieldDefinition(fieldDef).Signature;
    }

    internal void GetTypeRefPropsOrThrow(
        TypeReferenceHandle handle,
        out string name,
        out string @namespace,
        out EntityHandle resolutionScope) {
        var typeRef = metadataReader.GetTypeReference(handle);
        resolutionScope = typeRef.ResolutionScope;
        name = metadataReader.GetString(typeRef.Name);
        @namespace = metadataReader.GetString(typeRef.Namespace);
    }

    internal BlobReader GetTypeSpecificationSignatureReaderOrThrow(TypeSpecificationHandle typeSpec) {
        var signature = metadataReader.GetTypeSpecification(typeSpec).Signature;
        return metadataReader.GetBlobReader(signature);
    }

    internal string GetModuleRefNameOrThrow(ModuleReferenceHandle moduleRef) {
        return metadataReader.GetString(metadataReader.GetModuleReference(moduleRef).Name);
    }

    internal ParameterHandleCollection GetParametersOfMethodOrThrow(MethodDefinitionHandle methodDef) {
        return metadataReader.GetMethodDefinition(methodDef).GetParameters();
    }

    internal int GetParameterSequenceNumberOrThrow(ParameterHandle param) {
        return metadataReader.GetParameter(param).SequenceNumber;
    }
}
