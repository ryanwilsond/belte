using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    private class MethodUncommonData : UncommonData {
        internal static readonly MethodUncommonData NoApplicableOperators = new MethodUncommonData(
            conversionResult: UserDefinedConversionResult.NoApplicableOperators([]),
            conversionMethod: null
        );

        internal MethodUncommonData(MethodSymbol conversionMethod, UserDefinedConversionResult conversionResult) {
            this.conversionMethod = conversionMethod;
            this.conversionResult = conversionResult;
        }

        internal MethodSymbol conversionMethod { get; }

        internal UserDefinedConversionResult conversionResult { get; }
    }
}
