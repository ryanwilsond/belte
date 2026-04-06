using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class UIntTC : INumericTC<uint> {
        public static readonly UIntTC Instance = new UIntTC();

        uint INumericTC<uint>.minValue => uint.MinValue;

        uint INumericTC<uint>.maxValue => uint.MaxValue;

        uint INumericTC<uint>.zero => 0;

        public bool Related(BinaryOperatorKind relation, uint left, uint right) {
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

        uint INumericTC<uint>.Next(uint value) {
            return value + 1;
        }

        public uint FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (uint)0 : (uint)constantValue.value;
        }

        public ConstantValue ToConstantValue(uint value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.UInt32);
        }

        string INumericTC<uint>.ToString(uint value) {
            return value.ToString();
        }

        uint INumericTC<uint>.Prev(uint value) {
            return value - 1;
        }
    }
}
