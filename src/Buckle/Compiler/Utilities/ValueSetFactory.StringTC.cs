using Buckle.CodeAnalysis;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private class StringTC : IEquatableValueTC<string> {
        public static readonly StringTC Instance = new StringTC();
        private StringTC() { }

        string IEquatableValueTC<string>.FromConstantValue(ConstantValue constantValue) {
            var result = constantValue is null ? string.Empty : (string)constantValue.value;
            return result;
        }

        ConstantValue IEquatableValueTC<string>.ToConstantValue(string value) {
            return new ConstantValue(value, CodeAnalysis.Symbols.SpecialType.String);
        }
    }
}
