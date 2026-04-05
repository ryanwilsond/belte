using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class CharTC : INumericTC<char> {
        public static readonly CharTC Instance = new CharTC();

        char INumericTC<char>.minValue => char.MinValue;

        char INumericTC<char>.maxValue => char.MaxValue;

        char INumericTC<char>.zero => (char)0;

        bool INumericTC<char>.Related(BinaryOperatorKind relation, char left, char right) {
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

        char INumericTC<char>.Next(char value) {
            return (char)(value + 1);
        }

        char INumericTC<char>.FromConstantValue(ConstantValue constantValue) {
            return constantValue is null ? (char)0 : (char)constantValue.value;
        }

        string INumericTC<char>.ToString(char c) {
            return DisplayText.FormatLiteral(c);
        }

        char INumericTC<char>.Prev(char value) {
            return (char)(value - 1);
        }

        ConstantValue INumericTC<char>.ToConstantValue(char value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.Char);
        }
    }
}
