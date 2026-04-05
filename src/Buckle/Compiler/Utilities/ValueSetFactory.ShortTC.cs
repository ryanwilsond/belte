using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class ShortTC : INumericTC<short> {
        public static readonly ShortTC Instance = new ShortTC();

        short INumericTC<short>.minValue => short.MinValue;

        short INumericTC<short>.maxValue => short.MaxValue;

        short INumericTC<short>.zero => 0;

        bool INumericTC<short>.Related(BinaryOperatorKind relation, short left, short right) {
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

        short INumericTC<short>.Next(short value) {
            return (short)(value + 1);
        }

        short INumericTC<short>.Prev(short value) {
            return (short)(value - 1);
        }

        short INumericTC<short>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (short)0 : (short)constantValue.value;
        }

        ConstantValue INumericTC<short>.ToConstantValue(short value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Int16);
        }

        string INumericTC<short>.ToString(short value) {
            return value.ToString();
        }
    }
}
