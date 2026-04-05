using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class ByteTC : INumericTC<byte> {
        public static readonly ByteTC Instance = new ByteTC();

        byte INumericTC<byte>.minValue => byte.MinValue;

        byte INumericTC<byte>.maxValue => byte.MaxValue;

        byte INumericTC<byte>.zero => 0;

        bool INumericTC<byte>.Related(BinaryOperatorKind relation, byte left, byte right) {
            switch (relation) {
                case Equal:
                    return left == right;
                case GreaterThanOrEqual:
                    return left >= right;
                case GreaterThan:
                    return left > right;
                case LessThanOrEqual:
                    return left <= right;
                case LessThan:
                    return left < right;
                default:
                    throw new ArgumentException("relation");
            }
        }

        byte INumericTC<byte>.Next(byte value) {
            return (byte)(value + 1);
        }

        byte INumericTC<byte>.Prev(byte value) {
            return (byte)(value - 1);
        }

        byte INumericTC<byte>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (byte)0 : (byte)constantValue.value;
        }

        ConstantValue INumericTC<byte>.ToConstantValue(byte value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.UInt8);
        }

        string INumericTC<byte>.ToString(byte value) {
            return value.ToString();
        }
    }
}
