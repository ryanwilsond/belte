using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class MetadataDecoder : TypeNameDecoder<PEModuleSymbol, TypeSymbol> {
    private readonly PENamedTypeSymbol _typeContextOpt;
    private readonly PEMethodSymbol _methodContextOpt;
    private readonly AssemblyIdentity _containingAssemblyIdentity;

    internal readonly PEModule module;

    internal MetadataDecoder(PEModuleSymbol moduleSymbol, PENamedTypeSymbol context)
        : this(moduleSymbol, context, null) { }

    internal MetadataDecoder(PEModuleSymbol moduleSymbol, PEMethodSymbol context)
        : this(moduleSymbol, (PENamedTypeSymbol)context.containingType, context) { }

    internal MetadataDecoder(PEModuleSymbol moduleSymbol) : this(moduleSymbol, null, null) { }

    private MetadataDecoder(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol typeContextOpt,
        PEMethodSymbol methodContextOpt)
        : this(
            moduleSymbol.module,
            (moduleSymbol.containingAssembly is PEAssemblySymbol) ? moduleSymbol.containingAssembly.identity : null,
            SymbolFactory.Instance,
            moduleSymbol) {
        _typeContextOpt = typeContextOpt;
        _methodContextOpt = methodContextOpt;
    }

    internal MetadataDecoder(
        PEModule module,
        AssemblyIdentity containingAssemblyIdentity,
        SymbolFactory<PEModuleSymbol, TypeSymbol> factory,
        PEModuleSymbol moduleSymbol)
        : base(factory, moduleSymbol) {
        this.module = module;
        _containingAssemblyIdentity = containingAssemblyIdentity;
    }

    internal PEModuleSymbol moduleSymbol => _moduleSymbol;

    private protected MethodDefinitionHandle GetMethodHandle(MethodSymbol method) {
        var peMethod = method as PEMethodSymbol;

        if (peMethod is not null && ReferenceEquals(peMethod.containingModule, _moduleSymbol))
            return peMethod.handle;

        return default;
    }

    internal bool IsTargetAttribute(
        CustomAttributeHandle customAttribute,
        string namespaceName,
        string typeName,
        bool ignoreCase = false) {
        try {
            return module.IsTargetAttribute(
                customAttribute,
                namespaceName,
                typeName,
                out var ctor,
                ignoreCase);
        } catch (BadImageFormatException) {
            return false;
        }
    }

    internal static void GetSignatureCountsOrThrow(
        PEModule module,
        MethodDefinitionHandle methodDef,
        out int parameterCount,
        out int typeParameterCount) {
        var signature = module.GetMethodSignatureOrThrow(methodDef);
        var signatureReader = DecodeSignatureHeaderOrThrow(module, signature, out var signatureHeader);
        GetSignatureCountsOrThrow(ref signatureReader, signatureHeader, out parameterCount, out typeParameterCount);
    }

    private static void GetSignatureCountsOrThrow(
        ref BlobReader signatureReader,
        SignatureHeader signatureHeader,
        out int parameterCount,
        out int typeParameterCount) {
        typeParameterCount = signatureHeader.IsGeneric ? signatureReader.ReadCompressedInteger() : 0;
        parameterCount = signatureReader.ReadCompressedInteger();
    }

    internal static bool IsOrClosedOverATypeFromAssemblies(
        TypeSymbol symbol,
        ImmutableArray<AssemblySymbol> assemblies) {
        switch (symbol.kind) {
            case SymbolKind.TemplateParameter:
                return false;
            case SymbolKind.ArrayType:
                return IsOrClosedOverATypeFromAssemblies(((ArrayTypeSymbol)symbol).elementType, assemblies);
            case SymbolKind.ErrorType:
                goto case SymbolKind.NamedType;
            case SymbolKind.NamedType:
                var namedType = (NamedTypeSymbol)symbol;
                var containingAssembly = symbol.originalDefinition.containingAssembly;
                int i;

                if (containingAssembly is not null) {
                    for (i = 0; i < assemblies.Length; i++) {
                        if (ReferenceEquals(containingAssembly, assemblies[i]))
                            return true;
                    }
                }

                do {
                    var arguments = namedType.templateArguments;
                    var count = arguments.Length;

                    for (i = 0; i < count; i++) {
                        if (IsOrClosedOverATypeFromAssemblies(arguments[i].type.type, assemblies))
                            return true;
                    }

                    namedType = namedType.containingType;
                } while (namedType is not null);

                return false;
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }
    }

    internal BlobReader DecodeSignatureHeaderOrThrow(BlobHandle signature, out SignatureHeader signatureHeader) {
        return DecodeSignatureHeaderOrThrow(module, signature, out signatureHeader);
    }

    internal static BlobReader DecodeSignatureHeaderOrThrow(
        PEModule module,
        BlobHandle signature,
        out SignatureHeader signatureHeader) {
        var reader = module.GetMemoryReaderOrThrow(signature);
        signatureHeader = reader.ReadSignatureHeader();
        return reader;
    }


    private ConcurrentDictionary<TypeDefinitionHandle, TypeSymbol> GetTypeHandleToTypeMap() {
        return _moduleSymbol.typeHandleToTypeMap;
    }

    private ConcurrentDictionary<TypeReferenceHandle, TypeSymbol> GetTypeRefHandleToTypeMap() {
        return _moduleSymbol.typeRefHandleToTypeMap;
    }

    private protected override TypeSymbol LookupNestedTypeDefSymbol(
        TypeSymbol container,
        ref MetadataTypeName emittedName) {
        var result = container.LookupMetadataType(ref emittedName);
        return result ?? new MissingMetadataTypeSymbol.Nested((NamedTypeSymbol)container, ref emittedName);
    }

    private protected override TypeSymbol LookupTopLevelTypeDefSymbol(
        int referencedAssemblyIndex,
        ref MetadataTypeName emittedName) {
        var assembly = _moduleSymbol.GetReferencedAssemblySymbol(referencedAssemblyIndex);

        if (assembly is null)
            return new UnsupportedMetadataTypeSymbol();

        try {
            return assembly.LookupDeclaredOrForwardedTopLevelMetadataType(ref emittedName, visitedAssemblies: null);
            // } catch (Exception e) when (FatalError.ReportAndPropagate(e))
        } catch {
            throw ExceptionUtilities.Unreachable();
        }
    }

    private TypeSymbol LookupTopLevelTypeDefSymbol(
        string moduleName,
        ref MetadataTypeName emittedName,
        out bool isNoPiaLocalType) {
        foreach (var m in _moduleSymbol.containingAssembly.modules) {
            if (string.Equals(m.name, moduleName, StringComparison.OrdinalIgnoreCase)) {
                if ((object)m == (object)_moduleSymbol) {
                    return _moduleSymbol.LookupTopLevelMetadataTypeWithNoPiaLocalTypeUnification(
                        ref emittedName,
                        out isNoPiaLocalType
                    );
                } else {
                    isNoPiaLocalType = false;
                    var result = m.LookupTopLevelMetadataType(ref emittedName);
                    return result ?? new MissingMetadataTypeSymbol.TopLevel(m, ref emittedName);
                }
            }
        }

        isNoPiaLocalType = false;
        return new MissingMetadataTypeSymbol.TopLevel(
            new MissingModuleSymbolWithName(_moduleSymbol.containingAssembly, moduleName),
            ref emittedName,
            SpecialType.None
        );
    }

    private protected override TypeSymbol LookupTopLevelTypeDefSymbol(
        ref MetadataTypeName emittedName,
        out bool isNoPiaLocalType) {
        return _moduleSymbol.LookupTopLevelMetadataTypeWithNoPiaLocalTypeUnification(
            ref emittedName,
            out isNoPiaLocalType
        );
    }

    private protected override bool IsContainingAssembly(AssemblyIdentity identity) {
        return _containingAssemblyIdentity is not null && _containingAssemblyIdentity.Equals(identity);
    }

    private protected override int GetIndexOfReferencedAssembly(AssemblyIdentity identity) {
        var assemblies = _moduleSymbol.referencedAssemblies;

        for (var i = 0; i < assemblies.Length; i++) {
            if (identity.Equals(assemblies[i]))
                return i;
        }

        return -1;
    }

    internal int GetTargetAttributeSignatureIndex(
        CustomAttributeHandle customAttribute,
        AttributeDescription description) {
        try {
            return module.GetTargetAttributeSignatureIndex(customAttribute, description);
        } catch (BadImageFormatException) {
            return -1;
        }
    }

    internal FieldInfo<TypeSymbol> DecodeFieldSignature(FieldDefinitionHandle fieldHandle) {
        try {
            var signature = module.GetFieldSignatureOrThrow(fieldHandle);
            var signatureReader = DecodeSignatureHeaderOrThrow(signature, out var signatureHeader);

            if (signatureHeader.Kind != SignatureKind.Field) {
                return new FieldInfo<TypeSymbol>(GetUnsupportedMetadataTypeSymbol());
            } else {
                return DecodeFieldSignature(ref signatureReader);
            }
        } catch (BadImageFormatException mrEx) {
            return new FieldInfo<TypeSymbol>(GetUnsupportedMetadataTypeSymbol(mrEx));
        }
    }

    private FieldInfo<TypeSymbol> DecodeFieldSignature(ref BlobReader signatureReader) {
        try {
            var isByRef = false;
            ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers = default;
            var customModifiers = DecodeModifiersOrThrow(
                ref signatureReader,
                out var typeCode
            );

            if (typeCode == SignatureTypeCode.ByReference) {
                isByRef = true;
                refCustomModifiers = customModifiers;
                customModifiers = DecodeModifiersOrThrow(
                    ref signatureReader,
                    out typeCode
                );
            }

            var type = DecodeTypeOrThrow(ref signatureReader, typeCode, out _);
            return new FieldInfo<TypeSymbol>(isByRef, refCustomModifiers, type, customModifiers);
        } catch (UnsupportedSignatureContent) {
            return new FieldInfo<TypeSymbol>(GetUnsupportedMetadataTypeSymbol());
        } catch (BadImageFormatException mrEx) {
            return new FieldInfo<TypeSymbol>(GetUnsupportedMetadataTypeSymbol(mrEx));
        }
    }

    private ImmutableArray<ModifierInfo<TypeSymbol>> DecodeModifiersOrThrow(
        ref BlobReader signatureReader,
        out SignatureTypeCode typeCode) {
        ArrayBuilder<ModifierInfo<TypeSymbol>> modifiers = null;

        for (; ; ) {
            typeCode = signatureReader.ReadSignatureTypeCode();
            bool isOptional;

            if (typeCode == SignatureTypeCode.RequiredModifier)
                isOptional = false;
            else if (typeCode == SignatureTypeCode.OptionalModifier)
                isOptional = true;
            else
                break;

            var type = DecodeModifierTypeOrThrow(ref signatureReader);
            var modifier = new ModifierInfo<TypeSymbol>(isOptional, type);

            modifiers ??= ArrayBuilder<ModifierInfo<TypeSymbol>>.GetInstance();
            modifiers.Add(modifier);
        }

        return modifiers?.ToImmutableAndFree() ?? default;
    }

    private TypeSymbol DecodeModifierTypeOrThrow(ref BlobReader signatureReader) {
        var token = signatureReader.ReadTypeHandle();
        TypeSymbol type;
        bool isNoPiaLocalType;

tryAgain:
        switch (token.Kind) {
            case HandleKind.TypeDefinition:
                type = GetTypeOfTypeDef((TypeDefinitionHandle)token, out isNoPiaLocalType, isContainingType: false);
                type = SubstituteWithUnboundIfGeneric(type);
                break;
            case HandleKind.TypeReference:
                type = GetTypeOfTypeRef((TypeReferenceHandle)token, out isNoPiaLocalType);
                type = SubstituteWithUnboundIfGeneric(type);
                break;
            case HandleKind.TypeSpecification:
                var memoryReader = module.GetTypeSpecificationSignatureReaderOrThrow((TypeSpecificationHandle)token);
                SignatureTypeCode typeCode = memoryReader.ReadSignatureTypeCode();

                switch (typeCode) {
                    case SignatureTypeCode.Void:
                    case SignatureTypeCode.Boolean:
                    case SignatureTypeCode.SByte:
                    case SignatureTypeCode.Byte:
                    case SignatureTypeCode.Int16:
                    case SignatureTypeCode.UInt16:
                    case SignatureTypeCode.Int32:
                    case SignatureTypeCode.UInt32:
                    case SignatureTypeCode.Int64:
                    case SignatureTypeCode.UInt64:
                    case SignatureTypeCode.Single:
                    case SignatureTypeCode.Double:
                    case SignatureTypeCode.Char:
                    case SignatureTypeCode.String:
                    case SignatureTypeCode.IntPtr:
                    case SignatureTypeCode.UIntPtr:
                    case SignatureTypeCode.Object:
                    case SignatureTypeCode.TypedReference:
                        type = GetSpecialType(typeCode.ToSpecialType());
                        break;
                    case SignatureTypeCode.TypeHandle:
                        token = memoryReader.ReadTypeHandle();
                        goto tryAgain;
                    case SignatureTypeCode.GenericTypeInstance:
                        type = DecodeGenericTypeInstanceOrThrow(ref memoryReader, out var refersToNoPiaLocalType);
                        break;
                    default:
                        throw new UnsupportedSignatureContent();
                }

                break;
            default:
                throw new UnsupportedSignatureContent();
        }

        return type;
    }

    private TypeSymbol DecodeTypeOrThrow(
        ref BlobReader ppSig,
        SignatureTypeCode typeCode,
        out bool refersToNoPiaLocalType) {
        TypeSymbol typeSymbol;
        int paramPosition;
        ImmutableArray<ModifierInfo<TypeSymbol>> modifiers;

        refersToNoPiaLocalType = false;

        switch (typeCode) {
            case SignatureTypeCode.Void:
            case SignatureTypeCode.Boolean:
            case SignatureTypeCode.SByte:
            case SignatureTypeCode.Byte:
            case SignatureTypeCode.Int16:
            case SignatureTypeCode.UInt16:
            case SignatureTypeCode.Int32:
            case SignatureTypeCode.UInt32:
            case SignatureTypeCode.Int64:
            case SignatureTypeCode.UInt64:
            case SignatureTypeCode.Single:
            case SignatureTypeCode.Double:
            case SignatureTypeCode.Char:
            case SignatureTypeCode.String:
            case SignatureTypeCode.IntPtr:
            case SignatureTypeCode.UIntPtr:
            case SignatureTypeCode.Object:
            case SignatureTypeCode.TypedReference:
                typeSymbol = GetSpecialType(typeCode.ToSpecialType());
                break;
            case SignatureTypeCode.TypeHandle:
                typeSymbol = GetSymbolForTypeHandleOrThrow(
                    ppSig.ReadTypeHandle(),
                    out refersToNoPiaLocalType,
                    allowTypeSpec: false,
                    requireShortForm: true
                );

                break;
            case SignatureTypeCode.Array:
                int countOfDimensions;
                int countOfSizes;
                int countOfLowerBounds;

                modifiers = DecodeModifiersOrThrow(ref ppSig, out typeCode);
                typeSymbol = DecodeTypeOrThrow(ref ppSig, typeCode, out refersToNoPiaLocalType);

                if (!ppSig.TryReadCompressedInteger(out countOfDimensions) ||
                    !ppSig.TryReadCompressedInteger(out countOfSizes)) {
                    throw new UnsupportedSignatureContent();
                }

                ImmutableArray<int> sizes;

                if (countOfSizes == 0) {
                    sizes = [];
                } else {
                    var builder = ArrayBuilder<int>.GetInstance(countOfSizes);

                    for (var i = 0; i < countOfSizes; i++) {
                        if (ppSig.TryReadCompressedInteger(out var size))
                            builder.Add(size);
                        else
                            throw new UnsupportedSignatureContent();
                    }

                    sizes = builder.ToImmutableAndFree();
                }

                if (!ppSig.TryReadCompressedInteger(out countOfLowerBounds))
                    throw new UnsupportedSignatureContent();

                ImmutableArray<int> lowerBounds = default;

                if (countOfLowerBounds == 0) {
                    lowerBounds = [];
                } else {
                    var builder = countOfLowerBounds != countOfDimensions ? ArrayBuilder<int>.GetInstance(countOfLowerBounds, 0) : null;

                    for (var i = 0; i < countOfLowerBounds; i++) {
                        if (ppSig.TryReadCompressedSignedInteger(out var lowerBound)) {
                            if (lowerBound != 0) {
                                builder ??= ArrayBuilder<int>.GetInstance(countOfLowerBounds, 0);
                                builder[i] = lowerBound;
                            }
                        } else {
                            throw new UnsupportedSignatureContent();
                        }
                    }

                    if (builder is not null)
                        lowerBounds = builder.ToImmutableAndFree();
                }

                typeSymbol = GetMDArrayTypeSymbol(countOfDimensions, typeSymbol, modifiers, sizes, lowerBounds);
                break;
            case SignatureTypeCode.SZArray:
                modifiers = DecodeModifiersOrThrow(ref ppSig, out typeCode);
                typeSymbol = DecodeTypeOrThrow(ref ppSig, typeCode, out refersToNoPiaLocalType);
                typeSymbol = GetSZArrayTypeSymbol(typeSymbol, modifiers);
                break;
            case SignatureTypeCode.Pointer:
                modifiers = DecodeModifiersOrThrow(ref ppSig, out typeCode);
                typeSymbol = DecodeTypeOrThrow(ref ppSig, typeCode, out refersToNoPiaLocalType);
                typeSymbol = MakePointerTypeSymbol(typeSymbol, modifiers);
                break;
            case SignatureTypeCode.GenericTypeParameter:
                if (!ppSig.TryReadCompressedInteger(out paramPosition))
                    throw new UnsupportedSignatureContent();

                typeSymbol = GetGenericTypeParamSymbol(paramPosition);
                break;
            case SignatureTypeCode.GenericMethodParameter:
                if (!ppSig.TryReadCompressedInteger(out paramPosition))
                    throw new UnsupportedSignatureContent();

                typeSymbol = GetGenericMethodTypeParamSymbol(paramPosition);
                break;
            case SignatureTypeCode.GenericTypeInstance:
                typeSymbol = DecodeGenericTypeInstanceOrThrow(ref ppSig, out refersToNoPiaLocalType);
                break;
            case SignatureTypeCode.FunctionPointer:
                var signatureHeader = ppSig.ReadSignatureHeader();
                var parameters = DecodeSignatureParametersOrThrow(
                    ref ppSig,
                    signatureHeader,
                    typeParameterCount: out var typeParamCount,
                    shouldProcessAllBytes: false,
                    isFunctionPointerSignature: true
                );

                if (typeParamCount != 0)
                    throw new UnsupportedSignatureContent();

                typeSymbol = MakeFunctionPointerTypeSymbol(
                    CallingConventionUtilities.FromSignatureConvention(signatureHeader.CallingConvention),
                    ImmutableArray.Create(parameters)
                );

                break;
            default:
                throw new UnsupportedSignatureContent();
        }

        return typeSymbol;
    }

    private TypeSymbol GetGenericTypeParamSymbol(int position) {
        var type = _typeContextOpt;

        while (type is not null && (type.metadataArity - type.arity) > position)
            type = type.containingSymbol as PENamedTypeSymbol;

        if (type is null || type.metadataArity <= position)
            return new UnsupportedMetadataTypeSymbol();

        position -= type.metadataArity - type.arity;
        return type.templateParameters[position];
    }

    private TypeSymbol GetGenericMethodTypeParamSymbol(int position) {
        if (_methodContextOpt is null)
            return new UnsupportedMetadataTypeSymbol();

        var typeParameters = _methodContextOpt.templateParameters;

        if (typeParameters.Length <= position)
            return new UnsupportedMetadataTypeSymbol();

        return typeParameters[position];
    }

    internal TypeSymbol GetSymbolForTypeHandleOrThrow(
        EntityHandle handle,
        out bool isNoPiaLocalType,
        bool allowTypeSpec,
        bool requireShortForm) {
        if (handle.IsNil)
            throw new UnsupportedSignatureContent();

        TypeSymbol typeSymbol;
        switch (handle.Kind) {
            case HandleKind.TypeDefinition:
                typeSymbol = GetTypeOfTypeDef((TypeDefinitionHandle)handle, out isNoPiaLocalType, isContainingType: false);
                break;
            case HandleKind.TypeReference:
                typeSymbol = GetTypeOfTypeRef((TypeReferenceHandle)handle, out isNoPiaLocalType);
                break;
            case HandleKind.TypeSpecification:
                if (!allowTypeSpec)
                    throw new UnsupportedSignatureContent();

                isNoPiaLocalType = false;
                typeSymbol = GetTypeOfTypeSpec((TypeSpecificationHandle)handle);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(handle.Kind);
        }

        if (requireShortForm && typeSymbol.specialType.HasShortFormSignatureEncoding())
            throw new UnsupportedSignatureContent();

        return typeSymbol;
    }

    private TypeSymbol GetTypeOfTypeDef(
        TypeDefinitionHandle typeDef,
        out bool isNoPiaLocalType,
        bool isContainingType) {
        try {
            var cache = GetTypeHandleToTypeMap();

            if (cache is not null && cache.TryGetValue(typeDef, out var result)) {
                if (!module.IsNestedTypeDefOrThrow(typeDef) && module.IsNoPiaLocalType(typeDef))
                    isNoPiaLocalType = true;
                else
                    isNoPiaLocalType = false;

                return result;
            }

            MetadataTypeName mdName;
            var name = module.GetTypeDefNameOrThrow(typeDef);

            if (module.IsNestedTypeDefOrThrow(typeDef)) {
                var containerTypeDef = module.GetContainingTypeOrThrow(typeDef);

                if (containerTypeDef.IsNil) {
                    isNoPiaLocalType = false;
                    return GetUnsupportedMetadataTypeSymbol();
                }

                var container = GetTypeOfTypeDef(containerTypeDef, out isNoPiaLocalType, isContainingType: true);

                if (isNoPiaLocalType) {
                    if (!isContainingType)
                        isNoPiaLocalType = false;

                    return GetUnsupportedMetadataTypeSymbol();
                }

                mdName = MetadataTypeName.FromTypeName(name);
                return LookupNestedTypeDefSymbol(container, ref mdName);
            }

            var namespaceName = module.GetTypeDefNamespaceOrThrow(typeDef);

            mdName = namespaceName.Length > 0
                ? MetadataTypeName.FromNamespaceAndTypeName(namespaceName, name)
                : MetadataTypeName.FromTypeName(name);

            if (module.IsNoPiaLocalType(
                typeDef,
                out var interfaceGuid,
                out var scope,
                out var identifier)) {
                isNoPiaLocalType = true;

                if (!module.HasGenericParametersOrThrow(typeDef)) {
                    var localTypeName = MetadataTypeName.FromNamespaceAndTypeName(mdName.namespaceName, mdName.typeName, forcedArity: 0);
                    result = SubstituteNoPiaLocalType(typeDef,
                        ref localTypeName,
                        interfaceGuid,
                        scope,
                        identifier);
                    return result;
                }

                result = GetUnsupportedMetadataTypeSymbol();

                if (cache is not null)
                    result = cache.GetOrAdd(typeDef, result);

                return result;
            }

            isNoPiaLocalType = false;
            result = LookupTopLevelTypeDefSymbol(ref mdName, out isNoPiaLocalType);
            return result;
        } catch (BadImageFormatException mrEx) {
            isNoPiaLocalType = false;
            return GetUnsupportedMetadataTypeSymbol(mrEx);
        }
    }

    private TypeSymbol SubstituteNoPiaLocalType(
        TypeDefinitionHandle typeDef,
        ref MetadataTypeName name,
        string interfaceGuid,
        string scope,
        string identifier) {
        TypeSymbol result;
        try {
            bool isInterface = module.IsInterfaceOrThrow(typeDef);
            TypeSymbol baseType = null;

            if (!isInterface) {
                var baseToken = module.GetBaseTypeOfTypeOrThrow(typeDef);

                if (!baseToken.IsNil)
                    baseType = GetTypeOfToken(baseToken);
            }

            result = SubstituteNoPiaLocalType(
                ref name,
                isInterface,
                baseType,
                interfaceGuid,
                scope,
                identifier,
                moduleSymbol.containingAssembly
            );
        } catch (BadImageFormatException mrEx) {
            result = GetUnsupportedMetadataTypeSymbol(mrEx);
        }

        var cache = GetTypeHandleToTypeMap();
        var newresult = cache.GetOrAdd(typeDef, result);
        return newresult;
    }

    internal static NamedTypeSymbol SubstituteNoPiaLocalType(
        ref MetadataTypeName name,
        bool isInterface,
        TypeSymbol baseType,
        string interfaceGuid,
        string scope,
        string identifier,
        AssemblySymbol referringAssembly) {
        NamedTypeSymbol result = null;

        var interfaceGuidValue = new Guid();
        var haveInterfaceGuidValue = false;
        var scopeGuidValue = new Guid();
        var haveScopeGuidValue = false;

        if (isInterface && interfaceGuid is not null) {
            haveInterfaceGuidValue = Guid.TryParse(interfaceGuid, out interfaceGuidValue);

            if (haveInterfaceGuidValue) {
                scope = null;
                identifier = null;
            }
        }

        if (scope is not null)
            haveScopeGuidValue = Guid.TryParse(scope, out scopeGuidValue);

        foreach (var assembly in referringAssembly.GetNoPiaResolutionAssemblies()) {
            if (ReferenceEquals(assembly, referringAssembly))
                continue;

            var candidate = assembly.LookupDeclaredTopLevelMetadataType(ref name);

            if (candidate is null ||
                candidate.declaredAccessibility != Accessibility.Public) {
                continue;
            }

            var haveCandidateGuidValue = false;
            var candidateGuidValue = new Guid();

            switch (candidate.typeKind) {
                // case TypeKind.Interface:
                //     if (!isInterface) {
                //         continue;
                //     }

                //     // Get candidate's Guid
                //     if (candidate.GetGuidString(out candidateGuid) && candidateGuid != null) {
                //         haveCandidateGuidValue = Guid.TryParse(candidateGuid, out candidateGuidValue);
                //     }

                //     break;

                // case TypeKind.Delegate:
                case TypeKind.Enum:
                case TypeKind.Struct:
                    if (isInterface)
                        continue;

                    var baseSpecialType = candidate.baseType?.specialType ?? SpecialType.None;

                    if (baseSpecialType == SpecialType.None || baseSpecialType != (baseType?.specialType ?? SpecialType.None)) {
                        continue;
                    }

                    break;

                default:
                    continue;
            }

            if (haveInterfaceGuidValue || haveCandidateGuidValue) {
                if (!haveInterfaceGuidValue || !haveCandidateGuidValue ||
                    candidateGuidValue != interfaceGuidValue) {
                    continue;
                }
            } else {
                if (!haveScopeGuidValue || identifier == null || !identifier.Equals(name.fullName)) {
                    continue;
                }

                haveCandidateGuidValue = false;

                if (assembly.GetGuidString(out var candidateGuid) && candidateGuid is not null)
                    haveCandidateGuidValue = Guid.TryParse(candidateGuid, out candidateGuidValue);

                if (!haveCandidateGuidValue || scopeGuidValue != candidateGuidValue)
                    continue;
            }

            if (result is not null) {
                // TODO
                // result = new NoPiaAmbiguousCanonicalTypeSymbol(referringAssembly, result, candidate);
                break;
            }

            result = candidate;
        }

        // TODO
        // result ??= new NoPiaMissingCanonicalTypeSymbol(
        //     referringAssembly,
        //     name.fullName,
        //     interfaceGuid,
        //     scope,
        //     identifier
        // );

        return result;
    }

    private TypeSymbol GetTypeOfTypeRef(TypeReferenceHandle typeRef, out bool isNoPiaLocalType) {
        var cache = GetTypeRefHandleToTypeMap();

        if (cache is not null && cache.TryGetValue(typeRef, out var result)) {
            isNoPiaLocalType = false;
            return result;
        }

        try {
            module.GetTypeRefPropsOrThrow(typeRef, out var name, out var @namespace, out var resolutionScope);

            var mdName = @namespace.Length > 01
                ? MetadataTypeName.FromNamespaceAndTypeName(@namespace, name)
                : MetadataTypeName.FromTypeName(name);

            result = GetTypeByNameOrThrow(ref mdName, resolutionScope, out isNoPiaLocalType);
        } catch (BadImageFormatException mrEx) {
            result = GetUnsupportedMetadataTypeSymbol(mrEx);
            isNoPiaLocalType = false;
        }

        if (cache is not null && !isNoPiaLocalType) {
            var result1 = cache.GetOrAdd(typeRef, result);
        }

        return result;
    }

    private TypeSymbol GetTypeByNameOrThrow(
        ref MetadataTypeName fullName,
        EntityHandle tokenResolutionScope,
        out bool isNoPiaLocalType) {
        var tokenType = tokenResolutionScope.Kind;

        if (tokenType == HandleKind.TypeReference) {
            if (tokenResolutionScope.IsNil)
                throw new BadImageFormatException();

            var psymContainer = GetTypeOfToken(tokenResolutionScope);
            isNoPiaLocalType = false;
            return LookupNestedTypeDefSymbol(psymContainer, ref fullName);
        }

        if (tokenType == HandleKind.AssemblyReference) {
            isNoPiaLocalType = false;
            var assemblyRef = (AssemblyReferenceHandle)tokenResolutionScope;

            if (assemblyRef.IsNil)
                throw new BadImageFormatException();

            return LookupTopLevelTypeDefSymbol(module.GetAssemblyReferenceIndexOrThrow(assemblyRef), ref fullName);
        }

        if (tokenType == HandleKind.ModuleReference) {
            var moduleRef = (ModuleReferenceHandle)tokenResolutionScope;

            if (moduleRef.IsNil)
                throw new BadImageFormatException();

            return LookupTopLevelTypeDefSymbol(
                module.GetModuleRefNameOrThrow(moduleRef),
                ref fullName,
                out isNoPiaLocalType
            );
        }

        if (tokenResolutionScope == EntityHandle.ModuleDefinition)
            return LookupTopLevelTypeDefSymbol(ref fullName, out isNoPiaLocalType);

        isNoPiaLocalType = false;
        return GetUnsupportedMetadataTypeSymbol();
    }

    internal TypeSymbol GetTypeOfToken(EntityHandle token) {
        return GetTypeOfToken(token, out _);
    }

    internal TypeSymbol GetTypeOfToken(EntityHandle token, out bool isNoPiaLocalType) {
        TypeSymbol type;
        var tokenType = token.Kind;

        switch (tokenType) {
            case HandleKind.TypeDefinition:
                type = GetTypeOfTypeDef((TypeDefinitionHandle)token, out isNoPiaLocalType, isContainingType: false);
                break;
            case HandleKind.TypeSpecification:
                isNoPiaLocalType = false;
                type = GetTypeOfTypeSpec((TypeSpecificationHandle)token);
                break;
            case HandleKind.TypeReference:
                type = GetTypeOfTypeRef((TypeReferenceHandle)token, out isNoPiaLocalType);
                break;
            default:
                isNoPiaLocalType = false;
                type = GetUnsupportedMetadataTypeSymbol();
                break;
        }

        return type;
    }

    private TypeSymbol GetTypeOfTypeSpec(TypeSpecificationHandle typeSpec) {
        TypeSymbol ptype;

        try {
            var memoryReader = module.GetTypeSpecificationSignatureReaderOrThrow(typeSpec);
            ptype = DecodeTypeOrThrow(ref memoryReader, out var refersToNoPiaLocalType);
        } catch (BadImageFormatException mrEx) {
            ptype = GetUnsupportedMetadataTypeSymbol(mrEx);
        } catch (UnsupportedSignatureContent) {
            ptype = GetUnsupportedMetadataTypeSymbol();
        }

        return ptype;
    }

    private TypeSymbol DecodeTypeOrThrow(ref BlobReader ppSig, out bool refersToNoPiaLocalType) {
        var typeCode = ppSig.ReadSignatureTypeCode();
        return DecodeTypeOrThrow(ref ppSig, typeCode, out refersToNoPiaLocalType);
    }

    private TypeSymbol DecodeGenericTypeInstanceOrThrow(ref BlobReader ppSig, out bool refersToNoPiaLocalType) {
        var elementTypeCode = ppSig.ReadSignatureTypeCode();

        if (elementTypeCode != SignatureTypeCode.TypeHandle)
            throw new UnsupportedSignatureContent();

        var tokenGeneric = ppSig.ReadTypeHandle();

        if (!ppSig.TryReadCompressedInteger(out var argumentCount))
            throw new UnsupportedSignatureContent();

        var generic = GetTypeOfToken(tokenGeneric, out refersToNoPiaLocalType);

        var argumentsBuilder = ArrayBuilder<KeyValuePair<TypeSymbol, ImmutableArray<ModifierInfo<TypeSymbol>>>>
            .GetInstance(argumentCount);

        var argumentRefersToNoPiaLocalTypeBuilder = ArrayBuilder<bool>.GetInstance(argumentCount);

        for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++) {
            var modifiers = DecodeModifiersOrThrow(ref ppSig, out var typeCode);

            argumentsBuilder.Add(KeyValuePairUtilities.Create(
                DecodeTypeOrThrow(ref ppSig, typeCode, out var argumentRefersToNoPia),
                modifiers
            ));

            argumentRefersToNoPiaLocalTypeBuilder.Add(argumentRefersToNoPia);
        }

        var arguments = argumentsBuilder.ToImmutableAndFree();
        var argumentRefersToNoPiaLocalType = argumentRefersToNoPiaLocalTypeBuilder.ToImmutableAndFree();
        var typeSymbol = SubstituteTypeParameters(generic, arguments, argumentRefersToNoPiaLocalType);

        foreach (var flag in argumentRefersToNoPiaLocalType) {
            if (flag) {
                refersToNoPiaLocalType = true;
                break;
            }
        }

        return typeSymbol;
    }

    internal ParamInfo<TypeSymbol>[] GetSignatureForMethod(
        MethodDefinitionHandle methodDef,
        out SignatureHeader signatureHeader,
        out BadImageFormatException metadataException,
        bool setParamHandles = true) {
        ParamInfo<TypeSymbol>[] paramInfo = null;
        signatureHeader = default;

        try {
            var signature = module.GetMethodSignatureOrThrow(methodDef);
            var signatureReader = DecodeSignatureHeaderOrThrow(signature, out signatureHeader);

            paramInfo = DecodeSignatureParametersOrThrow(
                ref signatureReader,
                signatureHeader,
                out var typeParameterCount
            );

            if (setParamHandles) {
                var paramInfoLength = paramInfo.Length;

                foreach (var param in module.GetParametersOfMethodOrThrow(methodDef)) {
                    var sequenceNumber = module.GetParameterSequenceNumberOrThrow(param);

                    if (sequenceNumber >= 0 &&
                        sequenceNumber < paramInfoLength &&
                        paramInfo[sequenceNumber].handle.IsNil) {
                        paramInfo[sequenceNumber].handle = param;
                    }
                }
            }

            metadataException = null;
        } catch (BadImageFormatException mrEx) {
            metadataException = mrEx;

            if (paramInfo is null) {
                paramInfo = new ParamInfo<TypeSymbol>[1];
                paramInfo[0].type = GetUnsupportedMetadataTypeSymbol(mrEx);
            }
        }

        return paramInfo;
    }

    private ParamInfo<TypeSymbol>[] DecodeSignatureParametersOrThrow(
        ref BlobReader signatureReader,
        SignatureHeader signatureHeader,
        out int typeParameterCount,
        bool shouldProcessAllBytes = true,
        bool isFunctionPointerSignature = false) {
        GetSignatureCountsOrThrow(ref signatureReader, signatureHeader, out var paramCount, out typeParameterCount);

        var paramInfo = new ParamInfo<TypeSymbol>[paramCount + 1];
        uint paramIndex = 0;

        try {
            DecodeParameterOrThrow(ref signatureReader, ref paramInfo[0]);

            for (paramIndex = 1; paramIndex <= paramCount; paramIndex++)
                DecodeParameterOrThrow(ref signatureReader, ref paramInfo[paramIndex]);

            if (shouldProcessAllBytes && signatureReader.RemainingBytes > 0)
                throw new UnsupportedSignatureContent();
        } catch (Exception e) when ((e is UnsupportedSignatureContent ||
            e is BadImageFormatException) && !isFunctionPointerSignature) {
            for (; paramIndex <= paramCount; paramIndex++)
                paramInfo[paramIndex].type = GetUnsupportedMetadataTypeSymbol(e as BadImageFormatException);
        }

        return paramInfo;
    }

    private void DecodeParameterOrThrow(ref BlobReader signatureReader, ref ParamInfo<TypeSymbol> info) {
        info.customModifiers = DecodeModifiersOrThrow(
            ref signatureReader,
            out var typeCode);

        if (typeCode == SignatureTypeCode.ByReference) {
            info.isByRef = true;
            info.refCustomModifiers = info.customModifiers;
            info.customModifiers = DecodeModifiersOrThrow(ref signatureReader, out typeCode);
        }

        info.type = DecodeTypeOrThrow(ref signatureReader, typeCode, out _);
    }
}
