using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding {
    internal readonly struct BinaryOperatorAnalysisResult {
        internal readonly Conversion leftConversion;
        internal readonly Conversion rightConversion;
        internal readonly BinaryOperatorSignature signature;
        internal readonly OperatorAnalysisResultKind kind;

        private BinaryOperatorAnalysisResult(
            OperatorAnalysisResultKind kind,
            BinaryOperatorSignature signature,
            Conversion leftConversion,
            Conversion rightConversion) {
            this.kind = kind;
            this.signature = signature;
            this.leftConversion = leftConversion;
            this.rightConversion = rightConversion;
        }

        internal bool isValid => kind == OperatorAnalysisResultKind.Applicable;

        internal bool hasValue => kind != OperatorAnalysisResultKind.Undefined;

        public override bool Equals(object obj) {
            throw ExceptionUtilities.Unreachable();
        }

        public override int GetHashCode() {
            throw ExceptionUtilities.Unreachable();
        }

        internal static BinaryOperatorAnalysisResult Applicable(
            BinaryOperatorSignature signature,
            Conversion leftConversion,
            Conversion rightConversion) {
            return new BinaryOperatorAnalysisResult(
                OperatorAnalysisResultKind.Applicable,
                signature,
                leftConversion,
                rightConversion
            );
        }

        internal static BinaryOperatorAnalysisResult Inapplicable(
            BinaryOperatorSignature signature,
            Conversion leftConversion,
            Conversion rightConversion) {
            return new BinaryOperatorAnalysisResult(
                OperatorAnalysisResultKind.Inapplicable,
                signature,
                leftConversion,
                rightConversion
            );
        }

        internal BinaryOperatorAnalysisResult Worse() {
            return new BinaryOperatorAnalysisResult(
                OperatorAnalysisResultKind.Worse,
                signature,
                leftConversion,
                rightConversion
            );
        }
    }
}
