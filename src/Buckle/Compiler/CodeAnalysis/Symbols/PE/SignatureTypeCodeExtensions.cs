using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SignatureTypeCodeExtensions {
    internal static SpecialType ToSpecialType(this SignatureTypeCode typeCode) {
        return typeCode switch {
            SignatureTypeCode.Void => SpecialType.Void,
            SignatureTypeCode.Boolean => SpecialType.Bool,
            SignatureTypeCode.Int64 => SpecialType.Int,
            SignatureTypeCode.Double => SpecialType.Decimal,
            SignatureTypeCode.Char => SpecialType.Char,
            SignatureTypeCode.String => SpecialType.String,
            SignatureTypeCode.Object => SpecialType.Object,
            _ => throw ExceptionUtilities.UnexpectedValue(typeCode),
        };
    }
}
