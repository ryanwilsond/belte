using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class EnumConstantHelper {
    internal static EnumOverflowKind OffsetValue(
        ConstantValue constantValue,
        uint offset,
        out ConstantValue offsetValue) {
        offsetValue = null;

        EnumOverflowKind overflowKind;
        switch (constantValue.specialType) {
            case SpecialType.Int8: {
                    var previous = (long)(sbyte)constantValue.value;
                    overflowKind = CheckOverflow(sbyte.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((sbyte)(previous + offset), SpecialType.Int8);
                }

                break;
            case SpecialType.UInt8: {
                    var previous = (ulong)(byte)constantValue.value;
                    overflowKind = CheckOverflow(byte.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((byte)(previous + offset), SpecialType.UInt8);
                }

                break;
            case SpecialType.Int16: {
                    var previous = (long)(short)constantValue.value;
                    overflowKind = CheckOverflow(short.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((short)(previous + offset), SpecialType.Int16);
                }

                break;
            case SpecialType.UInt16: {
                    var previous = (ulong)(ushort)constantValue.value;
                    overflowKind = CheckOverflow(ushort.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((ushort)(previous + offset), SpecialType.UInt16);
                }

                break;
            case SpecialType.Int32: {
                    var previous = (long)(int)constantValue.value;
                    overflowKind = CheckOverflow(int.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((int)(previous + offset), SpecialType.Int32);
                }

                break;
            case SpecialType.UInt32: {
                    var previous = (ulong)(uint)constantValue.value;
                    overflowKind = CheckOverflow(uint.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((uint)(previous + offset), SpecialType.UInt32);
                }

                break;
            case SpecialType.Int:
            case SpecialType.Int64: {
                    var previous = (long)constantValue.value;
                    overflowKind = CheckOverflow(long.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((long)(previous + offset), SpecialType.Int64);
                }

                break;
            case SpecialType.UInt64: {
                    var previous = (ulong)constantValue.value;
                    overflowKind = CheckOverflow(ulong.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((ulong)(previous + offset), SpecialType.UInt64);
                }

                break;
            case SpecialType.Char: {
                    var previous = (ulong)(char)constantValue.value;
                    overflowKind = CheckOverflow(ulong.MaxValue, previous, offset);

                    if (overflowKind == EnumOverflowKind.NoOverflow)
                        offsetValue = new ConstantValue((ulong)(previous + offset), SpecialType.Char);
                }

                break;
            case SpecialType.String: {
                    overflowKind = EnumOverflowKind.NoOverflow;
                    offsetValue = constantValue;
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(constantValue.specialType);
        }

        return overflowKind;
    }

    private static EnumOverflowKind CheckOverflow(long maxOffset, long previous, uint offset) {
        return CheckOverflow(unchecked((ulong)(maxOffset - previous)), offset);
    }

    private static EnumOverflowKind CheckOverflow(ulong maxOffset, ulong previous, uint offset) {
        return CheckOverflow(maxOffset - previous, offset);
    }

    private static EnumOverflowKind CheckOverflow(ulong maxOffset, uint offset) {
        return (offset <= maxOffset)
            ? EnumOverflowKind.NoOverflow
            : (((offset - 1) == maxOffset) ? EnumOverflowKind.OverflowReport : EnumOverflowKind.OverflowIgnore
        );
    }
}
