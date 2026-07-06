using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Evaluating;

internal static class ValueKindExtensions {
    internal static ValueKind FromSpecialType(SpecialType specialType, ValueKind? def = null) {
        return specialType switch {
            SpecialType.Int => ValueKind.Int64,
            SpecialType.Int8 => ValueKind.Int8,
            SpecialType.Int16 => ValueKind.Int16,
            SpecialType.Int32 => ValueKind.Int32,
            SpecialType.Int64 => ValueKind.Int64,
            SpecialType.UInt8 => ValueKind.UInt8,
            SpecialType.UInt16 => ValueKind.UInt16,
            SpecialType.UInt32 => ValueKind.UInt32,
            SpecialType.UInt64 => ValueKind.UInt64,
            SpecialType.Float32 => ValueKind.Float32,
            SpecialType.Float64 => ValueKind.Float64,
            SpecialType.Decimal => ValueKind.Float64,
            SpecialType.Bool => ValueKind.Bool,
            SpecialType.WinBool => ValueKind.Int32,
            SpecialType.String => ValueKind.String,
            SpecialType.Char => ValueKind.Char,
            _ => def ?? throw ExceptionUtilities.UnexpectedValue(specialType),
        };
    }

    internal static SpecialType ToSpecialType(ValueKind valueKind) {
        return valueKind switch {
            ValueKind.Int8 => SpecialType.Int8,
            ValueKind.Int16 => SpecialType.Int16,
            ValueKind.Int32 => SpecialType.Int32,
            ValueKind.Int64 => SpecialType.Int64,
            ValueKind.UInt8 => SpecialType.UInt8,
            ValueKind.UInt16 => SpecialType.UInt16,
            ValueKind.UInt32 => SpecialType.UInt32,
            ValueKind.UInt64 => SpecialType.UInt64,
            ValueKind.Float32 => SpecialType.Float32,
            ValueKind.Float64 => SpecialType.Float64,
            ValueKind.Bool => SpecialType.Bool,
            ValueKind.String => SpecialType.String,
            ValueKind.Char => SpecialType.Char,
            _ => throw ExceptionUtilities.UnexpectedValue(valueKind),
        };
    }
}
