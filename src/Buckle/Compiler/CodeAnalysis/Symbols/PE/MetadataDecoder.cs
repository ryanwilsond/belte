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
        // TODO
        refersToNoPiaLocalType = false;
        return null;
    }

    private TypeSymbol GetTypeOfTypeDef(
        TypeDefinitionHandle typeDef,
        out bool isNoPiaLocalType,
        bool isContainingType) {
        // TODO
        isNoPiaLocalType = false;
        return null;
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
