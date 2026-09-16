using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypeExtensions {
    internal static SerializationTypeCode ToSerializationType(this SpecialType specialType) {
        var result = ToSerializationTypeOrInvalid(specialType);

        if (result == SerializationTypeCode.Invalid)
            throw ExceptionUtilities.UnexpectedValue(specialType);

        return result;
    }

    internal static SerializationTypeCode ToSerializationTypeOrInvalid(this SpecialType specialType) {
        switch (specialType) {
            case SpecialType.Bool:
                return SerializationTypeCode.Boolean;
            case SpecialType.Int8:
                return SerializationTypeCode.SByte;
            case SpecialType.UInt8:
                return SerializationTypeCode.Byte;
            case SpecialType.Int16:
                return SerializationTypeCode.Int16;
            case SpecialType.Int32:
                return SerializationTypeCode.Int32;
            case SpecialType.Int64:
            case SpecialType.Int:
                return SerializationTypeCode.Int64;
            case SpecialType.UInt16:
                return SerializationTypeCode.UInt16;
            case SpecialType.UInt32:
                return SerializationTypeCode.UInt32;
            case SpecialType.UInt64:
                return SerializationTypeCode.UInt64;
            case SpecialType.Float32:
                return SerializationTypeCode.Single;
            case SpecialType.Float64:
            case SpecialType.Decimal:
                return SerializationTypeCode.Double;
            case SpecialType.Char:
                return SerializationTypeCode.Char;
            case SpecialType.String:
                return SerializationTypeCode.String;
            case SpecialType.Object:
                return SerializationTypeCode.TaggedObject;
            default:
                return SerializationTypeCode.Invalid;
        }
    }
}
