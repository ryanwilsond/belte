
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEModuleSymbol : NonMissingModuleSymbol {
    private const int DefaultTypeMapCapacity = 31;

    private readonly AssemblySymbol _assemblySymbol;
    private readonly int _ordinal;
    private readonly PEModule _module;
    private readonly PENamespaceSymbol _globalNamespace;

    internal readonly ConcurrentDictionary<TypeDefinitionHandle, TypeSymbol> typeHandleToTypeMap =
        new ConcurrentDictionary<TypeDefinitionHandle, TypeSymbol>(concurrencyLevel: 2, capacity: DefaultTypeMapCapacity);

    internal readonly ConcurrentDictionary<TypeReferenceHandle, TypeSymbol> typeRefHandleToTypeMap =
        new ConcurrentDictionary<TypeReferenceHandle, TypeSymbol>(concurrencyLevel: 2, capacity: DefaultTypeMapCapacity);

    internal readonly ImmutableArray<MetadataLocation> metadataLocation;
    internal readonly MetadataImportOptions importOptions;

    private ImmutableArray<AttributeData> _lazyCustomAttributes;
    private ImmutableArray<AttributeData> _lazyAssemblyAttributes;

    private ICollection<string> _lazyTypeNames;
    private ICollection<string> _lazyNamespaceNames;

    private NullableMemberMetadata _lazyNullableMemberMetadata;

    private RefSafetyRulesAttributeVersion _lazyRefSafetyRulesAttributeVersion;

    internal PEModuleSymbol(
        PEAssemblySymbol assemblySymbol,
        PEModule module,
        MetadataImportOptions importOptions,
        int ordinal)
        : this((AssemblySymbol)assemblySymbol, module, importOptions, ordinal) {
    }

    internal PEModuleSymbol(
        SourceAssemblySymbol assemblySymbol,
        PEModule module,
        MetadataImportOptions importOptions,
        int ordinal)
        : this((AssemblySymbol)assemblySymbol, module, importOptions, ordinal) {
    }

    private PEModuleSymbol(
        AssemblySymbol assemblySymbol,
        PEModule module,
        MetadataImportOptions importOptions,
        int ordinal) {
        _assemblySymbol = assemblySymbol;
        _ordinal = ordinal;
        _module = module;
        this.importOptions = importOptions;
        _globalNamespace = new PEGlobalNamespaceSymbol(this);
        metadataLocation = [new MetadataLocation(this)];
    }

    public override string name => _module.name;

    internal sealed override bool areLocalsZeroed => throw ExceptionUtilities.Unreachable();

    internal override int ordinal => _ordinal;

    internal override bool bit32Required => _module.bit32Required;

    internal PEModule module => _module;

    internal override NamespaceSymbol globalNamespace => _globalNamespace;

    private static EntityHandle Token => EntityHandle.ModuleDefinition;

    internal override Symbol containingSymbol => _assemblySymbol;

    internal override AssemblySymbol containingAssembly => _assemblySymbol;

    internal override ImmutableArray<TextLocation> locations => metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override ImmutableArray<AttributeData> GetAttributes() {
        if (_lazyCustomAttributes.IsDefault)
            LoadCustomAttributes(Token, ref _lazyCustomAttributes);

        return _lazyCustomAttributes;
    }

    internal ImmutableArray<AttributeData> GetAssemblyAttributes() {
        if (_lazyAssemblyAttributes.IsDefault) {
            ArrayBuilder<AttributeData> moduleAssemblyAttributesBuilder = null;

            // TODO CorLibrary interop
            // string corlibName = containingAssembly.CorLibrary.Name;
            // EntityHandle assemblyMSCorLib = module.GetAssemblyRef(corlibName);
            // if (!assemblyMSCorLib.IsNil) {
            //     foreach (var qualifier in Cci.MetadataWriter.dummyAssemblyAttributeParentQualifier) {
            //         EntityHandle typerefAssemblyAttributesGoHere =
            //                     Module.GetTypeRef(
            //                         assemblyMSCorLib,
            //                         Cci.MetadataWriter.dummyAssemblyAttributeParentNamespace,
            //                         Cci.MetadataWriter.dummyAssemblyAttributeParentName + qualifier);

            //         if (!typerefAssemblyAttributesGoHere.IsNil) {
            //             try {
            //                 foreach (var customAttributeHandle in Module.GetCustomAttributesOrThrow(typerefAssemblyAttributesGoHere)) {
            //                     if (moduleAssemblyAttributesBuilder == null) {
            //                         moduleAssemblyAttributesBuilder = new ArrayBuilder<AttributeData>();
            //                     }
            //                     moduleAssemblyAttributesBuilder.Add(new PEAttributeData(this, customAttributeHandle));
            //                 }
            //             } catch (BadImageFormatException) { }
            //         }
            //     }
            // }

            ImmutableInterlocked.InterlockedCompareExchange(
                ref _lazyAssemblyAttributes,
                (moduleAssemblyAttributesBuilder is not null)
                    ? moduleAssemblyAttributesBuilder.ToImmutableAndFree()
                    : [],
                default
            );
        }
        return _lazyAssemblyAttributes;
    }

    internal void LoadCustomAttributes(EntityHandle token, ref ImmutableArray<AttributeData> customAttributes) {
        var loaded = GetCustomAttributesForToken(token);
        ImmutableInterlocked.InterlockedInitialize(ref customAttributes, loaded);
    }

    internal void LoadCustomAttributesFilterExtensions(EntityHandle token,
        ref ImmutableArray<AttributeData> customAttributes) {
        var loadedCustomAttributes = GetCustomAttributesFilterCompilerAttributes(token, out _, out _);
        ImmutableInterlocked.InterlockedInitialize(ref customAttributes, loadedCustomAttributes);
    }

    internal ImmutableArray<AttributeData> GetCustomAttributesForToken(EntityHandle token,
        out CustomAttributeHandle filteredOutAttribute1,
        AttributeDescription filterOut1) {
        return GetCustomAttributesForToken(token, out filteredOutAttribute1, filterOut1, out _, default, out _, default, out _, default, out _, default, out _, default);
    }

    internal ImmutableArray<AttributeData> GetCustomAttributesForToken(EntityHandle token,
        out CustomAttributeHandle filteredOutAttribute1,
        AttributeDescription filterOut1,
        out CustomAttributeHandle filteredOutAttribute2,
        AttributeDescription filterOut2) {
        return GetCustomAttributesForToken(token, out filteredOutAttribute1, filterOut1, out filteredOutAttribute2, filterOut2, out _, default, out _, default, out _, default, out _, default);
    }

    internal ImmutableArray<AttributeData> GetCustomAttributesForToken(EntityHandle token,
        out CustomAttributeHandle filteredOutAttribute1,
        AttributeDescription filterOut1,
        out CustomAttributeHandle filteredOutAttribute2,
        AttributeDescription filterOut2,
        out CustomAttributeHandle filteredOutAttribute3,
        AttributeDescription filterOut3,
        out CustomAttributeHandle filteredOutAttribute4,
        AttributeDescription filterOut4,
        out CustomAttributeHandle filteredOutAttribute5,
        AttributeDescription filterOut5,
        out CustomAttributeHandle filteredOutAttribute6,
        AttributeDescription filterOut6) {
        filteredOutAttribute1 = default;
        filteredOutAttribute2 = default;
        filteredOutAttribute3 = default;
        filteredOutAttribute4 = default;
        filteredOutAttribute5 = default;
        filteredOutAttribute6 = default;
        ArrayBuilder<AttributeData> customAttributesBuilder = null;

        try {
            foreach (var customAttributeHandle in _module.GetCustomAttributesOrThrow(token)) {
                if (MatchesFilter(customAttributeHandle, filterOut1)) {
                    filteredOutAttribute1 = customAttributeHandle;
                    continue;
                }

                if (MatchesFilter(customAttributeHandle, filterOut2)) {
                    filteredOutAttribute2 = customAttributeHandle;
                    continue;
                }

                if (MatchesFilter(customAttributeHandle, filterOut3)) {
                    filteredOutAttribute3 = customAttributeHandle;
                    continue;
                }

                if (MatchesFilter(customAttributeHandle, filterOut4)) {
                    filteredOutAttribute4 = customAttributeHandle;
                    continue;
                }

                if (MatchesFilter(customAttributeHandle, filterOut5)) {
                    filteredOutAttribute5 = customAttributeHandle;
                    continue;
                }

                if (MatchesFilter(customAttributeHandle, filterOut6)) {
                    filteredOutAttribute6 = customAttributeHandle;
                    continue;
                }

                if (customAttributesBuilder == null) {
                    customAttributesBuilder = ArrayBuilder<AttributeData>.GetInstance();
                }

                customAttributesBuilder.Add(new PEAttributeData(this, customAttributeHandle));
            }
        } catch (BadImageFormatException) { }

        if (customAttributesBuilder is not null)
            return customAttributesBuilder.ToImmutableAndFree();

        return [];

        bool MatchesFilter(CustomAttributeHandle handle, AttributeDescription filter)
            => filter.signatures is not null && module.GetTargetAttributeSignatureIndex(handle, filter) != -1;
    }

    internal ImmutableArray<AttributeData> GetCustomAttributesForToken(EntityHandle token) {
        return GetCustomAttributesForToken(token, out _, default);
    }

    internal ImmutableArray<AttributeData> GetCustomAttributesForToken(EntityHandle token,
        out CustomAttributeHandle paramArrayAttribute) {
        return GetCustomAttributesForToken(token, out paramArrayAttribute, AttributeDescription.ParamArrayAttribute);
    }

    internal bool HasAnyCustomAttributes(EntityHandle token) {
        try {
            foreach (var attr in _module.GetCustomAttributesOrThrow(token)) {
                return true;
            }
        } catch (BadImageFormatException) { }

        return false;
    }

    internal TypeSymbol TryDecodeAttributeWithTypeArgument(EntityHandle handle, AttributeDescription attributeDescription) {
        if (_module.HasStringValuedAttribute(handle, attributeDescription, out var typeName))
            return new MetadataDecoder(this).GetTypeSymbolForSerializedType(typeName);

        return null;
    }

    private ImmutableArray<AttributeData> GetCustomAttributesFilterCompilerAttributes(
        EntityHandle token,
        out bool foundExtension,
        out bool foundReadOnly) {
        var result = GetCustomAttributesForToken(
            token,
            filteredOutAttribute1: out var extensionAttribute,
            filterOut1: AttributeDescription.CaseSensitiveExtensionAttribute,
            filteredOutAttribute2: out var isReadOnlyAttribute,
            filterOut2: AttributeDescription.IsReadOnlyAttribute
        );

        foundExtension = !extensionAttribute.IsNil;
        foundReadOnly = !isReadOnlyAttribute.IsNil;
        return result;
    }

    internal void OnNewTypeDeclarationsLoaded(
        Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>> typesDict) {
        // TODO CorLib interop
        // bool keepLookingForDeclaredCorTypes = _ordinal == 0 && _assemblySymbol.KeepLookingForDeclaredSpecialTypes;

        // foreach (var types in typesDict.Values) {
        //     foreach (var type in types) {
        //         bool added;
        //         added = typeHandleToTypeMap.TryAdd(type.handle, type);

        //         if (keepLookingForDeclaredCorTypes && type.specialType != SpecialType.None) {
        //             _assemblySymbol.RegisterDeclaredSpecialType(type);
        //             keepLookingForDeclaredCorTypes = _assemblySymbol.KeepLookingForDeclaredSpecialTypes;
        //         }
        //     }
        // }
    }

    internal override ICollection<string> typeNames {
        get {
            if (_lazyTypeNames is null)
                Interlocked.CompareExchange(ref _lazyTypeNames, _module.typeNames.AsCaseSensitiveCollection(), null);

            return _lazyTypeNames;
        }
    }

    internal override ICollection<string> namespaceNames {
        get {
            if (_lazyNamespaceNames is null) {
                Interlocked.CompareExchange(
                    ref _lazyNamespaceNames,
                    _module.namespaceNames.AsCaseSensitiveCollection(),
                    null
                );
            }

            return _lazyNamespaceNames;
        }
    }

    internal override ImmutableArray<byte> GetHash(AssemblyHashAlgorithm algorithmId) {
        return _module.GetHash(algorithmId);
    }

    internal override bool hasAssemblyCompilationRelaxationsAttribute {
        get {
            var assemblyAttributes = GetAssemblyAttributes();
            return assemblyAttributes.IndexOfAttribute(AttributeDescription.CompilationRelaxationsAttribute) >= 0;
        }
    }

    internal override bool hasAssemblyRuntimeCompatibilityAttribute {
        get {
            var assemblyAttributes = GetAssemblyAttributes();
            return assemblyAttributes.IndexOfAttribute(AttributeDescription.RuntimeCompatibilityAttribute) >= 0;
        }
    }

    internal override CharSet? defaultMarshallingCharSet => throw ExceptionUtilities.Unreachable();

    internal sealed override Compilation declaringCompilation => null;

    internal override bool useUpdatedEscapeRules
        => refSafetyRulesVersion == RefSafetyRulesAttributeVersion.Version11;

    internal RefSafetyRulesAttributeVersion refSafetyRulesVersion {
        get {
            if (_lazyRefSafetyRulesAttributeVersion == RefSafetyRulesAttributeVersion.Uninitialized)
                _lazyRefSafetyRulesAttributeVersion = GetAttributeVersion();

            return _lazyRefSafetyRulesAttributeVersion;

            RefSafetyRulesAttributeVersion GetAttributeVersion() {
                if (_module.HasRefSafetyRulesAttribute(Token, out var version, out var foundAttributeType)) {
                    return version == 11
                        ? RefSafetyRulesAttributeVersion.Version11
                        : RefSafetyRulesAttributeVersion.UnrecognizedAttribute;
                }

                return foundAttributeType
                    ? RefSafetyRulesAttributeVersion.UnrecognizedAttribute
                    : RefSafetyRulesAttributeVersion.NoAttribute;
            }
        }
    }

    internal NamedTypeSymbol LookupTopLevelMetadataTypeWithNoPiaLocalTypeUnification(
        ref MetadataTypeName emittedName,
        out bool isNoPiaLocalType) {
        NamedTypeSymbol result;
        var scope = (PENamespaceSymbol)globalNamespace.LookupNestedNamespace(emittedName.namespaceSegmentsMemory);

        if (scope is null) {
            result = null;
        } else {
            result = scope.LookupMetadataType(ref emittedName);

            if (result is null) {
                result = scope.UnifyIfNoPiaLocalType(ref emittedName);

                if (result is not null) {
                    isNoPiaLocalType = true;
                    return result;
                }
            }
        }

        isNoPiaLocalType = false;
        return result ?? new MissingMetadataTypeSymbol.TopLevel(this, ref emittedName);
    }

#nullable disable

    /// <summary>
    /// Returns a tuple of the assemblies this module forwards the given type to.
    /// </summary>
    /// <param name="fullName">Type to look up.</param>
    /// <returns>A tuple of the forwarded to assemblies.</returns>
    /// <remarks>
    /// The returned assemblies may also forward the type.
    /// </remarks>
    internal (AssemblySymbol FirstSymbol, AssemblySymbol SecondSymbol) GetAssembliesForForwardedType(ref MetadataTypeName fullName) {
        var (firstIndex, secondIndex) = module.GetAssemblyRefsForForwardedType(
            fullName.fullName,
            ignoreCase: false,
            matchedName: out _
        );

        if (firstIndex < 0)
            return (null, null);

        var firstSymbol = GetReferencedAssemblySymbol(firstIndex);

        if (secondIndex < 0)
            return (firstSymbol, null);

        var secondSymbol = GetReferencedAssemblySymbol(secondIndex);
        return (firstSymbol, secondSymbol);
    }

    internal IEnumerable<NamedTypeSymbol> GetForwardedTypes() {
        foreach (var forwarder in module.GetForwardedTypes()) {
            var name = MetadataTypeName.FromFullName(forwarder.Key);

            var firstSymbol = GetReferencedAssemblySymbol(forwarder.Value.FirstIndex);

            if (forwarder.Value.SecondIndex >= 0) {
                var secondSymbol = GetReferencedAssemblySymbol(forwarder.Value.SecondIndex);

                yield return containingAssembly.CreateMultipleForwardingErrorTypeSymbol(
                    ref name,
                    this,
                    firstSymbol,
                    secondSymbol
                );
            } else {
                yield return firstSymbol.LookupDeclaredOrForwardedTopLevelMetadataType(ref name, null);
            }
        }
    }

    internal override ModuleMetadata GetMetadata() => _module.GetNonDisposableMetadata();

    internal bool ShouldDecodeNullableAttributes(Symbol symbol) {
        if (_lazyNullableMemberMetadata == NullableMemberMetadata.Unknown) {
            _lazyNullableMemberMetadata = _module.HasNullablePublicOnlyAttribute(Token, out bool includesInternals)
                ? (includesInternals ? NullableMemberMetadata.Internal : NullableMemberMetadata.Public)
                : NullableMemberMetadata.All;
        }

        var nullableMemberMetadata = _lazyNullableMemberMetadata;

        if (nullableMemberMetadata == NullableMemberMetadata.All)
            return true;

        if (AccessCheck.IsEffectivelyPublicOrInternal(symbol, out var isInternal)) {
            return nullableMemberMetadata switch {
                NullableMemberMetadata.Public => !isInternal,
                NullableMemberMetadata.Internal => true,
                _ => throw ExceptionUtilities.UnexpectedValue(nullableMemberMetadata),
            };
        }

        return false;
    }
}
