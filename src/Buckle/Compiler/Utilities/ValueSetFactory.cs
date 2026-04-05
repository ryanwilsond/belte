using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    internal static readonly IValueSetFactory<byte> ForByte = new NumericValueSetFactory<byte>(ByteTC.Instance);
    internal static readonly IValueSetFactory<sbyte> ForSByte = new NumericValueSetFactory<sbyte>(SByteTC.Instance);
    internal static readonly IValueSetFactory<char> ForChar = new NumericValueSetFactory<char>(CharTC.Instance);
    internal static readonly IValueSetFactory<short> ForShort = new NumericValueSetFactory<short>(ShortTC.Instance);
    internal static readonly IValueSetFactory<ushort> ForUShort = new NumericValueSetFactory<ushort>(UShortTC.Instance);
    internal static readonly IValueSetFactory<int> ForInt = new NumericValueSetFactory<int>(IntTC.DefaultInstance);
    internal static readonly IValueSetFactory<uint> ForUInt = new NumericValueSetFactory<uint>(UIntTC.Instance);
    internal static readonly IValueSetFactory<long> ForLong = new NumericValueSetFactory<long>(LongTC.Instance);
    internal static readonly IValueSetFactory<ulong> ForULong = new NumericValueSetFactory<ulong>(ULongTC.Instance);
    internal static readonly IValueSetFactory<bool> ForBool = BoolValueSetFactory.Instance;
    internal static readonly IValueSetFactory<float> ForFloat = new FloatingValueSetFactory<float>(SingleTC.Instance);
    internal static readonly IValueSetFactory<double> ForDouble = new FloatingValueSetFactory<double>(DoubleTC.Instance);
    internal static readonly IValueSetFactory<string> ForString = new EnumeratedValueSetFactory<string>(StringTC.Instance);

    internal static IValueSetFactory ForSpecialType(SpecialType specialType) {
        return specialType switch {
            SpecialType.UInt8 => ForByte,
            SpecialType.Int8 => ForSByte,
            SpecialType.Char => ForChar,
            SpecialType.Int16 => ForShort,
            SpecialType.UInt16 => ForUShort,
            SpecialType.Int32 => ForInt,
            SpecialType.UInt32 => ForUInt,
            SpecialType.Int64 => ForLong,
            SpecialType.UInt64 => ForULong,
            SpecialType.Bool => ForBool,
            SpecialType.Float32 => ForFloat,
            SpecialType.Float64 => ForDouble,
            SpecialType.String => ForString,
            SpecialType.Decimal => ForDouble,
            SpecialType.Int => ForLong,
            _ => null,
        };
    }

    internal static IValueSetFactory ForType(TypeSymbol type) {
        type = type.EnumUnderlyingTypeOrSelf();
        return ForSpecialType(type.specialType);
    }

    internal static IValueSetFactory ForInput(BoundDagTemp input) {
        return ForType(input.type);
    }
}
