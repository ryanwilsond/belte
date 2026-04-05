using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class UShortTC : INumericTC<ushort> {
        public static readonly UShortTC Instance = new UShortTC();

        ushort INumericTC<ushort>.minValue => ushort.MinValue;

        ushort INumericTC<ushort>.maxValue => ushort.MaxValue;

        ushort INumericTC<ushort>.zero => 0;

        bool INumericTC<ushort>.Related(BinaryOperatorKind relation, ushort left, ushort right) {
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

        ushort INumericTC<ushort>.Next(ushort value) {
            return (ushort)(value + 1);
        }

        ushort INumericTC<ushort>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (ushort)0 : (ushort)constantValue.value;
        }

        ConstantValue INumericTC<ushort>.ToConstantValue(ushort value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.UInt16);
        }

        string INumericTC<ushort>.ToString(ushort value) {
            return value.ToString();
        }

        ushort INumericTC<ushort>.Prev(ushort value) {
            return (ushort)(value - 1);
        }
    }
}
