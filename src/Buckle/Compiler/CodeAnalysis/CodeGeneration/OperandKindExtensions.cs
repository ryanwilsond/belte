using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static class OperandKindExtensions {
    internal static bool IsLiteral(this OperandKind kind) {
        switch (kind) {
            case OperandKind.String:
            case OperandKind.UInt8:
            case OperandKind.UInt16:
            case OperandKind.Int8:
            case OperandKind.Int32:
            case OperandKind.Int64:
            case OperandKind.Float32:
            case OperandKind.Float64:
                return true;
            default:
                return false;
        }
    }

    internal static SpecialType ToSpecialType(this OperandKind kind) {
        return kind switch {
            OperandKind.String => SpecialType.String,
            OperandKind.UInt8 => SpecialType.UInt8,
            OperandKind.UInt16 => SpecialType.UInt16,
            OperandKind.Int8 => SpecialType.Int8,
            OperandKind.Int32 => SpecialType.Int32,
            OperandKind.Int64 => SpecialType.Int64,
            OperandKind.Float32 => SpecialType.Float32,
            OperandKind.Float64 => SpecialType.Float64,
            _ => SpecialType.None,
        };
    }
}
