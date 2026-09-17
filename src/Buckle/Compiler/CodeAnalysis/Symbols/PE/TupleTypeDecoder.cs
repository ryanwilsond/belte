using System.Collections.Immutable;
using System.Reflection.Metadata;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal struct TupleTypeDecoder {
    private readonly ImmutableArray<string> _elementNames;

    private int _namesIndex;
    private bool _foundUsableErrorType;
    private bool _decodingFailed;

    private TupleTypeDecoder(ImmutableArray<string> elementNames) {
        _elementNames = elementNames;
        _namesIndex = elementNames.IsDefault ? 0 : elementNames.Length;
        _decodingFailed = false;
        _foundUsableErrorType = false;
    }

    public static TypeSymbol DecodeTupleTypesIfApplicable(
        TypeSymbol metadataType,
        EntityHandle targetHandle,
        PEModuleSymbol containingModule) {
        var hasTupleElementNamesAttribute = containingModule
            .module
            .HasTupleElementNamesAttribute(targetHandle, out var elementNames);

        if (hasTupleElementNamesAttribute && elementNames.IsDefaultOrEmpty)
            return new UnsupportedMetadataTypeSymbol();

        return DecodeTupleTypesInternal(metadataType, elementNames, hasTupleElementNamesAttribute);
    }

    public static TypeWithAnnotations DecodeTupleTypesIfApplicable(
        TypeWithAnnotations metadataType,
        EntityHandle targetHandle,
        PEModuleSymbol containingModule) {
        var hasTupleElementNamesAttribute = containingModule
            .module
            .HasTupleElementNamesAttribute(targetHandle, out var elementNames);

        if (hasTupleElementNamesAttribute && elementNames.IsDefaultOrEmpty)
            return new TypeWithAnnotations(new UnsupportedMetadataTypeSymbol());

        var type = metadataType.type;
        var decoded = DecodeTupleTypesInternal(type, elementNames, hasTupleElementNamesAttribute);

        return (object)decoded == type
            ? metadataType
            : new TypeWithAnnotations(decoded, metadataType.isNullable);
    }

    public static TypeSymbol DecodeTupleTypesIfApplicable(
        TypeSymbol metadataType,
        ImmutableArray<string> elementNames) {
        return DecodeTupleTypesInternal(
            metadataType,
            elementNames,
            hasTupleElementNamesAttribute: !elementNames.IsDefaultOrEmpty
        );
    }

    private static TypeSymbol DecodeTupleTypesInternal(
        TypeSymbol metadataType,
        ImmutableArray<string> elementNames,
        bool hasTupleElementNamesAttribute) {
        var decoder = new TupleTypeDecoder(elementNames);
        var decoded = decoder.DecodeType(metadataType);

        if (!decoder._decodingFailed) {
            if (!hasTupleElementNamesAttribute || decoder._namesIndex == 0)
                return decoded;
        }

        if (decoder._foundUsableErrorType)
            return metadataType;

        return new UnsupportedMetadataTypeSymbol();
    }

    private TypeSymbol DecodeType(TypeSymbol type) {
        switch (type.kind) {
            case SymbolKind.ErrorType:
                _foundUsableErrorType = true;
                return type;
            case SymbolKind.TemplateParameter:
                return type;
            case SymbolKind.FunctionPointerType:
                return DecodeFunctionPointerType((FunctionPointerTypeSymbol)type);
            case SymbolKind.PointerType:
                return DecodePointerType((PointerTypeSymbol)type);
            case SymbolKind.NamedType:
                return DecodeNamedType((NamedTypeSymbol)type);
            case SymbolKind.ArrayType:
                return DecodeArrayType((ArrayTypeSymbol)type);
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private PointerTypeSymbol DecodePointerType(PointerTypeSymbol type) {
        return type.WithPointedAtType(DecodeTypeInternal(type.pointedAtTypeWithAnnotations));
    }

    private FunctionPointerTypeSymbol DecodeFunctionPointerType(FunctionPointerTypeSymbol type) {
        var parameterTypes = ImmutableArray<TypeWithAnnotations>.Empty;
        var paramsModified = false;

        if (type.signature.parameterCount > 0) {
            var paramsBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance(type.signature.parameterCount);

            for (var i = type.signature.parameterCount - 1; i >= 0; i--) {
                var param = type.signature.parameters[i];
                var decodedParam = DecodeTypeInternal(param.typeWithAnnotations);
                paramsModified = paramsModified || !decodedParam.IsSameAs(param.typeWithAnnotations);
                paramsBuilder.Add(decodedParam);
            }

            if (paramsModified) {
                paramsBuilder.ReverseContents();
                parameterTypes = paramsBuilder.ToImmutableAndFree();
            } else {
                parameterTypes = type.signature.parameterTypesWithAnnotations;
                paramsBuilder.Free();
            }
        }

        var decodedReturnType = DecodeTypeInternal(type.signature.returnTypeWithAnnotations);

        if (paramsModified || !decodedReturnType.IsSameAs(type.signature.returnTypeWithAnnotations)) {
            return type.SubstituteTypeSymbol(
                decodedReturnType,
                parameterTypes.SelectAsArray(p => new TypeOrConstant(p))
            );
        } else {
            return type;
        }
    }

    private NamedTypeSymbol DecodeNamedType(NamedTypeSymbol type) {
        var typeArgs = type.templateArguments;
        var decodedArgs = DecodeTypeArguments(typeArgs);

        var decodedType = type;

        var containingType = type.containingType;
        NamedTypeSymbol decodedContainingType;

        if (containingType is not null && containingType.isTemplateType)
            decodedContainingType = DecodeNamedType(containingType);
        else
            decodedContainingType = containingType;

        var containerChanged = !ReferenceEquals(decodedContainingType, containingType);
        var typeArgsChanged = typeArgs != decodedArgs;

        if (typeArgsChanged || containerChanged) {
            if (containerChanged) {
                decodedType = decodedType.originalDefinition.AsMember(decodedContainingType);
                return decodedType.ConstructIfGeneric(decodedArgs);
            }

            decodedType = type.constructedFrom.Construct(decodedArgs, unbound: false);
        }

        if (decodedType.isTupleType) {
            var tupleCardinality = decodedType.tupleElementTypes.Length;

            if (tupleCardinality > 0) {
                var elementNames = EatElementNamesIfAvailable(tupleCardinality);
                decodedType = NamedTypeSymbol.CreateTuple(decodedType, elementNames);
            }
        }

        return decodedType;
    }

    private ImmutableArray<TypeOrConstant> DecodeTypeArguments(ImmutableArray<TypeOrConstant> templateArgs) {
        if (templateArgs.IsEmpty)
            return templateArgs;

        var decodedArgs = ArrayBuilder<TypeOrConstant>.GetInstance(templateArgs.Length);
        var anyDecoded = false;

        for (var i = templateArgs.Length - 1; i >= 0; i--) {
            var templateArg = templateArgs[i];
            var decoded = DecodeTypeInternal(templateArg.type);
            anyDecoded |= !decoded.IsSameAs(templateArg.type);
            decodedArgs.Add(new TypeOrConstant(decoded));
        }

        if (!anyDecoded) {
            decodedArgs.Free();
            return templateArgs;
        }

        decodedArgs.ReverseContents();
        return decodedArgs.ToImmutableAndFree();
    }

    private ArrayTypeSymbol DecodeArrayType(ArrayTypeSymbol type) {
        var decodedElementType = DecodeTypeInternal(type.elementTypeWithAnnotations);
        return type.WithElementType(decodedElementType);
    }

    private TypeWithAnnotations DecodeTypeInternal(TypeWithAnnotations typeWithAnnotations) {
        var type = typeWithAnnotations.type;
        var decoded = DecodeType(type);

        return ReferenceEquals(decoded, type)
            ? typeWithAnnotations
            : new TypeWithAnnotations(decoded, typeWithAnnotations.isNullable);
    }

    private ImmutableArray<string> EatElementNamesIfAvailable(int numberOfElements) {
        if (_elementNames.IsDefault)
            return _elementNames;

        if (numberOfElements > _namesIndex) {
            _namesIndex = 0;
            _decodingFailed = true;
            return default;
        }

        var start = _namesIndex - numberOfElements;
        _namesIndex = start;
        var allNull = true;

        for (var i = 0; i < numberOfElements; i++) {
            if (_elementNames[start + i] is not null) {
                allNull = false;
                break;
            }
        }

        if (allNull)
            return default;

        return ImmutableArray.Create(_elementNames, start, numberOfElements);
    }
}
