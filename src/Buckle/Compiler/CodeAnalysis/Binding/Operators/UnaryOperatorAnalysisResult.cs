
namespace Buckle.CodeAnalysis.Binding;

internal readonly struct UnaryOperatorAnalysisResult {
    internal readonly UnaryOperatorSignature signature;
    internal readonly Conversion conversion;
    internal readonly OperatorAnalysisResultKind kind;

    private UnaryOperatorAnalysisResult(
        OperatorAnalysisResultKind kind,
        UnaryOperatorSignature signature,
        Conversion conversion) {
        this.kind = kind;
        this.signature = signature;
        this.conversion = conversion;
    }

    internal bool isValid => kind == OperatorAnalysisResultKind.Applicable;

    internal bool hasValue => kind != OperatorAnalysisResultKind.Undefined;

    internal static UnaryOperatorAnalysisResult Applicable(UnaryOperatorSignature signature, Conversion conversion) {
        return new UnaryOperatorAnalysisResult(OperatorAnalysisResultKind.Applicable, signature, conversion);
    }

    internal static UnaryOperatorAnalysisResult Inapplicable(UnaryOperatorSignature signature, Conversion conversion) {
        return new UnaryOperatorAnalysisResult(OperatorAnalysisResultKind.Inapplicable, signature, conversion);
    }

    internal UnaryOperatorAnalysisResult Worse() {
        return new UnaryOperatorAnalysisResult(OperatorAnalysisResultKind.Worse, signature, conversion);
    }
}
