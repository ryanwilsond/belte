using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SignatureTypeCodeExtensions {
    internal static SpecialType ToSpecialType(this SignatureTypeCode typeCode) {
        return typeCode switch {
            SignatureTypeCode.Void => SpecialType.Void,
            SignatureTypeCode.Boolean => SpecialType.Bool,
            SignatureTypeCode.SByte => SpecialType.Int8,
            SignatureTypeCode.Int16 => SpecialType.Int16,
            SignatureTypeCode.Int32 => SpecialType.Int32,
            SignatureTypeCode.Int64 => SpecialType.Int64,
            SignatureTypeCode.Byte => SpecialType.UInt8,
            SignatureTypeCode.UInt16 => SpecialType.UInt16,
            SignatureTypeCode.UInt32 => SpecialType.UInt32,
            SignatureTypeCode.UInt64 => SpecialType.UInt64,
            SignatureTypeCode.IntPtr => SpecialType.IntPtr,
            SignatureTypeCode.UIntPtr => SpecialType.UIntPtr,
            SignatureTypeCode.Single => SpecialType.Float32,
            SignatureTypeCode.Double => SpecialType.Float64,
            SignatureTypeCode.Char => SpecialType.Char,
            SignatureTypeCode.String => SpecialType.String,
            SignatureTypeCode.Object => SpecialType.Object,
            SignatureTypeCode.TypedReference => SpecialType.TypedReference,
            _ => throw ExceptionUtilities.UnexpectedValue(typeCode),
        };
    }
}
