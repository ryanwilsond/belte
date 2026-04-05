using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class SByteTC : INumericTC<sbyte> {
        public static readonly SByteTC Instance = new SByteTC();
        sbyte INumericTC<sbyte>.minValue => sbyte.MinValue;

        sbyte INumericTC<sbyte>.maxValue => sbyte.MaxValue;

        sbyte INumericTC<sbyte>.zero => 0;

        bool INumericTC<sbyte>.Related(BinaryOperatorKind relation, sbyte left, sbyte right) {
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

        sbyte INumericTC<sbyte>.Next(sbyte value) {
            return (sbyte)(value + 1);
        }

        sbyte INumericTC<sbyte>.Prev(sbyte value) {
            return (sbyte)(value - 1);
        }

        sbyte INumericTC<sbyte>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (sbyte)0 : (sbyte)constantValue.value;
        }

        public ConstantValue ToConstantValue(sbyte value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Int8);
        }

        string INumericTC<sbyte>.ToString(sbyte value) {
            return value.ToString();
        }
    }
}
