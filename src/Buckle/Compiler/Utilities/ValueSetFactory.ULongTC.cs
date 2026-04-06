using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class ULongTC : INumericTC<ulong> {
        public static readonly ULongTC Instance = new ULongTC();

        ulong INumericTC<ulong>.minValue => ulong.MinValue;

        ulong INumericTC<ulong>.maxValue => ulong.MaxValue;

        ulong INumericTC<ulong>.zero => 0;

        bool INumericTC<ulong>.Related(BinaryOperatorKind relation, ulong left, ulong right) {
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

        ulong INumericTC<ulong>.Next(ulong value) {
            return value + 1;
        }

        ulong INumericTC<ulong>.Prev(ulong value) {
            return value - 1;
        }

        ulong INumericTC<ulong>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? 0UL : (ulong)constantValue.value;
        }

        ConstantValue INumericTC<ulong>.ToConstantValue(ulong value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.UInt64);
        }

        string INumericTC<ulong>.ToString(ulong value) {
            return value.ToString();
        }
    }
}
