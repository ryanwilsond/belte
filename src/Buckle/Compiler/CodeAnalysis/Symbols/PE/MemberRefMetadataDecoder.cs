using System;
using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MemberRefMetadataDecoder : MetadataDecoder {
    private readonly TypeSymbol _containingType;

    internal MemberRefMetadataDecoder(
        PEModuleSymbol moduleSymbol,
        TypeSymbol containingType) :
        base(moduleSymbol, containingType as PENamedTypeSymbol) {
        _containingType = containingType;
    }

    private protected override TypeSymbol GetGenericMethodTypeParamSymbol(int position) {
        return IndexedTemplateParameterSymbol.GetTemplateParameter(position);
    }

    private protected override TypeSymbol GetGenericTypeParamSymbol(int position) {
        if (_containingType is PENamedTypeSymbol peType)
            return base.GetGenericTypeParamSymbol(position);

        if (_containingType is NamedTypeSymbol namedType) {
            GetGenericTypeParameterSymbol(position, namedType, out var cumulativeArity, out var typeParameter);

            if (typeParameter is not null)
                return typeParameter;
            else
                return new UnsupportedMetadataTypeSymbol();
        }

        return new UnsupportedMetadataTypeSymbol();
    }

    private static void GetGenericTypeParameterSymbol(
        int position,
        NamedTypeSymbol namedType,
        out int cumulativeArity,
        out TemplateParameterSymbol typeArgument) {
        cumulativeArity = namedType.arity;
        typeArgument = null;

        var arityOffset = 0;

        var containingType = namedType.containingType;

        if (containingType is not null) {
            GetGenericTypeParameterSymbol(
                position,
                containingType,
                out var containingTypeCumulativeArity,
                out typeArgument
            );

            cumulativeArity += containingTypeCumulativeArity;
            arityOffset = containingTypeCumulativeArity;
        }

        if (arityOffset <= position && position < cumulativeArity)
            typeArgument = namedType.templateParameters[position - arityOffset];
    }

    internal Symbol FindMember(EntityHandle memberRefOrMethodDef, bool methodsOnly) {
        try {
            string memberName;
            BlobHandle signatureHandle;

            switch (memberRefOrMethodDef.Kind) {
                case HandleKind.MemberReference:
                    var memberRef = (MemberReferenceHandle)memberRefOrMethodDef;
                    memberName = module.GetMemberRefNameOrThrow(memberRef);
                    signatureHandle = module.GetSignatureOrThrow(memberRef);
                    break;
                case HandleKind.MethodDefinition:
                    var methodDef = (MethodDefinitionHandle)memberRefOrMethodDef;
                    memberName = module.GetMethodDefNameOrThrow(methodDef);
                    signatureHandle = module.GetMethodSignatureOrThrow(methodDef);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(memberRefOrMethodDef.Kind);
            }

            var signaturePointer = DecodeSignatureHeaderOrThrow(signatureHandle, out var signatureHeader);

            switch (signatureHeader.RawValue & SignatureHeader.CallingConventionOrKindMask) {
                case (byte)SignatureCallingConvention.Default:
                case (byte)SignatureCallingConvention.VarArgs:
                    int typeParamCount;
                    var targetParamInfo = DecodeSignatureParametersOrThrow(
                        ref signaturePointer,
                        signatureHeader,
                        out typeParamCount
                    );

                    return FindMethodBySignature(
                        _containingType,
                        memberName,
                        signatureHeader,
                        typeParamCount,
                        targetParamInfo
                    );
                case (byte)SignatureKind.Field:
                    if (methodsOnly)
                        return null;

                    var fieldInfo = DecodeFieldSignature(ref signaturePointer);
                    return FindFieldBySignature(_containingType, memberName, fieldInfo);
                default:
                    return null;
            }
        } catch (BadImageFormatException) {
            return null;
        }
    }

    private static FieldSymbol FindFieldBySignature(
        TypeSymbol targetTypeSymbol,
        string targetMemberName,
        in FieldInfo<TypeSymbol> fieldInfo) {
        foreach (var member in targetTypeSymbol.GetMembers(targetMemberName)) {
            TypeWithAnnotations fieldType;

            if (member is FieldSymbol field &&
                field.refKind != RefKind.None == fieldInfo.isByRef &&
                // CustomModifiersMatch(field.refCustomModifiers, fieldInfo.refCustomModifiers) &&
                TypeSymbol.Equals((fieldType = field.typeWithAnnotations).type,
                    fieldInfo.type,
                    TypeCompareKind.CLRSignatureCompareOptions)
                // CustomModifiersMatch(fieldType.CustomModifiers, fieldInfo.CustomModifiers)
                ) {
                return field;
            }
        }

        return null;
    }

    private static MethodSymbol FindMethodBySignature(
        TypeSymbol targetTypeSymbol,
        string targetMemberName,
        SignatureHeader targetMemberSignatureHeader,
        int targetMemberTypeParamCount,
        ParamInfo<TypeSymbol>[] targetParamInfo) {
        foreach (var member in targetTypeSymbol.GetMembers(targetMemberName)) {
            if (member is MethodSymbol method &&
                ((byte)method.callingConvention == targetMemberSignatureHeader.RawValue) &&
                (targetMemberTypeParamCount == method.arity) &&
                MethodSymbolMatchesParamInfo(method, targetParamInfo)) {
                return method;
            }
        }

        return null;
    }

    private static bool MethodSymbolMatchesParamInfo(MethodSymbol candidateMethod, ParamInfo<TypeSymbol>[] targetParamInfo) {
        var numParams = targetParamInfo.Length - 1;

        if (candidateMethod.parameterCount != numParams)
            return false;

        var candidateMethodTypeMap = new TemplateMap(
            candidateMethod.templateParameters,
            IndexedTemplateParameterSymbol.Take(candidateMethod.arity)
        );

        if (!ReturnTypesMatch(candidateMethod, candidateMethodTypeMap, ref targetParamInfo[0]))
            return false;

        for (var i = 0; i < numParams; i++) {
            if (!ParametersMatch(
                candidateMethod.parameters[i],
                candidateMethodTypeMap,
                ref targetParamInfo[i + 1])) {
                return false;
            }
        }

        return true;
    }

    private static bool ParametersMatch(
        ParameterSymbol candidateParam,
        TemplateMap candidateMethodTypeMap,
        ref ParamInfo<TypeSymbol> targetParam) {
        if (candidateParam.refKind != RefKind.None != targetParam.isByRef)
            return false;

        var substituted = candidateParam.typeWithAnnotations.SubstituteType(candidateMethodTypeMap);

        if (!TypeSymbol.Equals(substituted.type.type, targetParam.type, TypeCompareKind.CLRSignatureCompareOptions))
            return false;

        // if (!CustomModifiersMatch(substituted.CustomModifiers, targetParam.CustomModifiers) ||
        //     !CustomModifiersMatch(candidateMethodTypeMap.SubstituteCustomModifiers(candidateParam.RefCustomModifiers), targetParam.RefCustomModifiers)) {
        //     return false;
        // }

        return true;
    }

    private static bool ReturnTypesMatch(
        MethodSymbol candidateMethod,
        TemplateMap candidateMethodTypeMap,
        ref ParamInfo<TypeSymbol> targetReturnParam) {
        if (candidateMethod.returnsByRef != targetReturnParam.isByRef)
            return false;

        var candidateMethodType = candidateMethod.returnTypeWithAnnotations;
        var targetReturnType = targetReturnParam.type;

        var substituted = candidateMethodType.SubstituteType(candidateMethodTypeMap);

        if (!TypeSymbol.Equals(substituted.type.type, targetReturnType, TypeCompareKind.CLRSignatureCompareOptions))
            return false;

        // if (!CustomModifiersMatch(substituted.CustomModifiers, targetReturnParam.CustomModifiers) ||
        //     !CustomModifiersMatch(candidateMethodTypeMap.SubstituteCustomModifiers(candidateMethod.RefCustomModifiers), targetReturnParam.RefCustomModifiers)) {
        //     return false;
        // }

        return true;
    }

    // private static bool CustomModifiersMatch(ImmutableArray<CustomModifier> candidateCustomModifiers, ImmutableArray<ModifierInfo<TypeSymbol>> targetCustomModifiers) {
    //     if (targetCustomModifiers.IsDefault || targetCustomModifiers.IsEmpty) {
    //         return candidateCustomModifiers.IsDefault || candidateCustomModifiers.IsEmpty;
    //     } else if (candidateCustomModifiers.IsDefault) {
    //         return false;
    //     }

    //     var n = candidateCustomModifiers.Length;
    //     if (targetCustomModifiers.Length != n) {
    //         return false;
    //     }

    //     for (int i = 0; i < n; i++) {
    //         var targetCustomModifier = targetCustomModifiers[i];
    //         CustomModifier candidateCustomModifier = candidateCustomModifiers[i];

    //         if (targetCustomModifier.IsOptional != candidateCustomModifier.IsOptional ||
    //             !object.Equals(targetCustomModifier.Modifier, ((CSharpCustomModifier)candidateCustomModifier).ModifierSymbol)) {
    //             return false;
    //         }
    //     }

    //     return true;
    // }
}
