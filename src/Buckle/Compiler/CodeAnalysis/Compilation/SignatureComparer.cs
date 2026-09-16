using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal abstract class SignatureComparer<MethodSymbol, FieldSymbol, PropertySymbol, TypeSymbol, ParameterSymbol>
    where MethodSymbol : class
    where FieldSymbol : class
    where PropertySymbol : class
    where TypeSymbol : class
    where ParameterSymbol : class {
    internal bool MatchFieldSignature(FieldSymbol field, ImmutableArray<byte> signature) {
        var position = 0;
        var result = MatchType(GetFieldType(field), signature, ref position);

        Debug.Assert(!result || position == signature.Length);
        return result;
    }

    internal bool MatchPropertySignature(PropertySymbol property, ImmutableArray<byte> signature) {
        var position = 0;
        var paramCount = signature[position++];
        var parameters = GetParameters(property);

        if (paramCount != parameters.Length)
            return false;

        var isByRef = IsByRef(signature, ref position);

        if (IsByRefProperty(property) != isByRef)
            return false;

        if (!MatchType(GetPropertyType(property), signature, ref position))
            return false;

        foreach (var parameter in parameters) {
            if (!MatchParameter(parameter, signature, ref position))
                return false;
        }

        Debug.Assert(position == signature.Length);
        return true;
    }

    internal bool MatchMethodSignature(MethodSymbol method, ImmutableArray<byte> signature) {
        var position = 0;
        int paramCount = signature[position++];
        var parameters = GetParameters(method);

        if (paramCount != parameters.Length)
            return false;

        var isByRef = IsByRef(signature, ref position);

        if (IsByRefMethod(method) != isByRef)
            return false;

        if (!MatchType(GetReturnType(method), signature, ref position))
            return false;

        foreach (var parameter in parameters) {
            if (!MatchParameter(parameter, signature, ref position))
                return false;
        }

        Debug.Assert(position == signature.Length);
        return true;
    }

    private bool MatchParameter(ParameterSymbol parameter, ImmutableArray<byte> signature, ref int position) {
        var isByRef = IsByRef(signature, ref position);

        if (IsByRefParam(parameter) != isByRef)
            return false;

        return MatchType(GetParamType(parameter), signature, ref position);
    }

    private static bool IsByRef(ImmutableArray<byte> signature, ref int position) {
        var typeCode = (SignatureTypeCode)signature[position];

        if (typeCode == SignatureTypeCode.ByReference) {
            position++;
            return true;
        } else {
            return false;
        }
    }

    private bool MatchType(TypeSymbol type, ImmutableArray<byte> signature, ref int position) {
        if (type is null)
            return false;

        int paramPosition;

        var typeCode = (SignatureTypeCode)signature[position++];

        switch (typeCode) {
            case SignatureTypeCode.TypeHandle:
                var expectedType = ReadTypeId(signature, ref position);
                return MatchTypeToTypeId(type, expectedType);
            case SignatureTypeCode.Array:
                if (!MatchType(GetMDArrayElementType(type), signature, ref position))
                    return false;

                int countOfDimensions = signature[position++];

                return MatchArrayRank(type, countOfDimensions);
            case SignatureTypeCode.SZArray:
                return MatchType(GetSZArrayElementType(type), signature, ref position);
            case SignatureTypeCode.Pointer:
                return MatchType(GetPointedToType(type), signature, ref position);
            case SignatureTypeCode.GenericTypeParameter:
                paramPosition = signature[position++];
                return IsGenericTypeParam(type, paramPosition);

            case SignatureTypeCode.GenericMethodParameter:
                paramPosition = signature[position++];
                return IsGenericMethodTypeParam(type, paramPosition);
            case SignatureTypeCode.GenericTypeInstance:
                if (!MatchType(GetGenericTypeDefinition(type), signature, ref position))
                    return false;

                int argumentCount = signature[position++];

                for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++) {
                    if (!MatchType(GetGenericTypeArgument(type, argumentIndex), signature, ref position))
                        return false;
                }

                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(typeCode);
        }
    }

    private static short ReadTypeId(ImmutableArray<byte> signature, ref int position) {
        var firstByte = signature[position++];

        if (firstByte == (byte)WellKnownType.ExtSentinel)
            return (short)(signature[position++] + WellKnownType.ExtSentinel);
        else
            return firstByte;
    }

    private protected abstract TypeSymbol GetGenericTypeArgument(TypeSymbol type, int argumentIndex);

    private protected abstract TypeSymbol GetGenericTypeDefinition(TypeSymbol type);

    private protected abstract bool IsGenericMethodTypeParam(TypeSymbol type, int paramPosition);

    private protected abstract bool IsGenericTypeParam(TypeSymbol type, int paramPosition);

    private protected abstract TypeSymbol GetPointedToType(TypeSymbol type);

    private protected abstract TypeSymbol GetSZArrayElementType(TypeSymbol type);

    private protected abstract bool MatchArrayRank(TypeSymbol type, int countOfDimensions);

    private protected abstract TypeSymbol GetMDArrayElementType(TypeSymbol type);

    private protected abstract bool MatchTypeToTypeId(TypeSymbol type, int typeId);

    private protected abstract TypeSymbol GetReturnType(MethodSymbol method);
    private protected abstract ImmutableArray<ParameterSymbol> GetParameters(MethodSymbol method);

    private protected abstract TypeSymbol GetPropertyType(PropertySymbol property);
    private protected abstract ImmutableArray<ParameterSymbol> GetParameters(PropertySymbol property);

    private protected abstract TypeSymbol GetParamType(ParameterSymbol parameter);

    private protected abstract bool IsByRefParam(ParameterSymbol parameter);
    private protected abstract bool IsByRefMethod(MethodSymbol method);
    private protected abstract bool IsByRefProperty(PropertySymbol property);

    private protected abstract TypeSymbol GetFieldType(FieldSymbol field);
}
