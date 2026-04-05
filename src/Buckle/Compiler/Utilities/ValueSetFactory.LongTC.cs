using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class LongTC : INumericTC<long> {
        public static readonly LongTC Instance = new LongTC();

        long INumericTC<long>.minValue => long.MinValue;

        long INumericTC<long>.maxValue => long.MaxValue;

        long INumericTC<long>.zero => 0;

        bool INumericTC<long>.Related(BinaryOperatorKind relation, long left, long right) {
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

        long INumericTC<long>.Next(long value) {
            return value + 1;
        }

        long INumericTC<long>.Prev(long value) {
            return value - 1;
        }

        long INumericTC<long>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? 0L : (long)constantValue.value;
        }

        ConstantValue INumericTC<long>.ToConstantValue(long value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Int64);
        }

        string INumericTC<long>.ToString(long value) {
            return value.ToString();
        }
    }
}
