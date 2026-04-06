using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class SingleTC : FloatingTC<float>, INumericTC<float> {
        public static readonly SingleTC Instance = new SingleTC();

        float INumericTC<float>.minValue => float.NegativeInfinity;

        float INumericTC<float>.maxValue => float.PositiveInfinity;

        float FloatingTC<float>.NaN => float.NaN;

        float INumericTC<float>.zero => 0;

        public float Next(float value) {
            if (value == 0)
                return float.Epsilon;

            if (value < 0) {
                if (value == -float.Epsilon)
                    return 0.0f;

                if (value == float.NegativeInfinity)
                    return float.MinValue;

                return -UintAsFloat(FloatAsUint(-value) - 1);
            }

            if (value == float.MaxValue)
                return float.PositiveInfinity;

            return UintAsFloat(FloatAsUint(value) + 1);
        }

        private static unsafe uint FloatAsUint(float d) {
            if (d == 0)
                return 0;

            float* dp = &d;
            uint* lp = (uint*)dp;
            return *lp;
        }

        private static unsafe float UintAsFloat(uint l) {
            uint* lp = &l;
            float* dp = (float*)lp;
            return *dp;
        }

        bool INumericTC<float>.Related(BinaryOperatorKind relation, float left, float right) {
            switch (relation) {
                case Equal:
                    return left == right || float.IsNaN(left) && float.IsNaN(right);
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

        float INumericTC<float>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? 0.0F : (float)constantValue.value;
        }

        ConstantValue INumericTC<float>.ToConstantValue(float value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Float32);
        }

        string INumericTC<float>.ToString(float value) {
            return float.IsNaN(value) ? "NaN" :
            value == float.NegativeInfinity ? "-Inf" :
            value == float.PositiveInfinity ? "Inf" :
            FormattableString.Invariant($"{value:G9}");
        }

        float INumericTC<float>.Prev(float value) {
            return -Next(-value);
        }
    }
}
