using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class DoubleTC : FloatingTC<double>, INumericTC<double> {
        public static readonly DoubleTC Instance = new DoubleTC();

        double INumericTC<double>.minValue => double.NegativeInfinity;

        double INumericTC<double>.maxValue => double.PositiveInfinity;

        double FloatingTC<double>.NaN => double.NaN;

        double INumericTC<double>.zero => 0.0;

        public double Next(double value) {
            if (value == 0)
                return double.Epsilon;

            if (value < 0) {
                if (value == -double.Epsilon)
                    return 0.0;
                if (value == double.NegativeInfinity)
                    return double.MinValue;

                return -ULongAsDouble(DoubleAsULong(-value) - 1);
            }

            if (value == double.MaxValue)
                return double.PositiveInfinity;

            return ULongAsDouble(DoubleAsULong(value) + 1);
        }

        private static ulong DoubleAsULong(double d) {
            if (d == 0)
                return 0;

            return (ulong)BitConverter.DoubleToInt64Bits(d);
        }

        private static double ULongAsDouble(ulong l) {
            return BitConverter.Int64BitsToDouble((long)l);
        }

        bool INumericTC<double>.Related(BinaryOperatorKind relation, double left, double right) {
            switch (relation) {
                case Equal:
                    return left == right || double.IsNaN(left) && double.IsNaN(right); // for our purposes, NaNs are equal
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

        double INumericTC<double>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? 0.0 : (double)constantValue.value;
        }

        ConstantValue INumericTC<double>.ToConstantValue(double value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Float64);
        }

        string INumericTC<double>.ToString(double value) {
            return double.IsNaN(value) ? "NaN" :
            value == double.NegativeInfinity ? "-Inf" :
            value == double.PositiveInfinity ? "Inf" :
            FormattableString.Invariant($"{value:G17}");
        }

        double INumericTC<double>.Prev(double value) {
            return -Next(-value);
        }
    }
}
