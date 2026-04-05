using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class IntTC : INumericTC<int> {
        public bool nonNegative;

        private IntTC(bool nonNegative) {
            this.nonNegative = nonNegative;
        }

        public static readonly IntTC DefaultInstance = new IntTC(nonNegative: false);
        public static readonly IntTC NonNegativeInstance = new IntTC(nonNegative: true);

        public int minValue => nonNegative ? 0 : int.MinValue;

        int INumericTC<int>.maxValue => int.MaxValue;

        int INumericTC<int>.zero => 0;

        public bool Related(BinaryOperatorKind relation, int left, int right) {
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

        int INumericTC<int>.Next(int value) {
            return value + 1;
        }

        int INumericTC<int>.Prev(int value) {
            return value - 1;
        }

        public int FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? 0 : (int)constantValue.value;
        }

        public ConstantValue ToConstantValue(int value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Int32);
        }

        string INumericTC<int>.ToString(int value) {
            return value.ToString();
        }
    }
}
