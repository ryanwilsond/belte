using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal static class UnOpEasyOut {
        private const UnaryOperatorKind ERR = UnaryOperatorKind.Error;
        private const UnaryOperatorKind INT = UnaryOperatorKind.Int;
        private const UnaryOperatorKind DEC = UnaryOperatorKind.Decimal;
        private const UnaryOperatorKind BOL = UnaryOperatorKind.Bool;
        private const UnaryOperatorKind LIN = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int;
        private const UnaryOperatorKind LDE = UnaryOperatorKind.Lifted | UnaryOperatorKind.Decimal;
        private const UnaryOperatorKind LBO = UnaryOperatorKind.Lifted | UnaryOperatorKind.Bool;

        private static readonly UnaryOperatorKind[] Increment = [
            //  any  str  bool chr  int  dec  type obj
            ERR, ERR, ERR, ERR, INT, DEC, ERR, ERR,
        ERR, ERR, ERR, ERR, LIN, LDE, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalIdentity = [
            //  any  str  bool chr  int  dec  type obj
            ERR, ERR, ERR, ERR, INT, DEC, ERR, ERR,
        ERR, ERR, ERR, ERR, LIN, LDE, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalNegation = [
            //  any  str  bool chr  int  dec  type obj
            ERR, ERR, ERR, ERR, INT, DEC, ERR, ERR,
        ERR, ERR, ERR, ERR, LIN, LDE, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BooleanNegation = [
            //  any  str  bool chr  int  dec  type obj
            ERR, ERR, BOL, ERR, ERR, ERR, ERR, ERR,
        ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BitwiseCompliment = [
            //  any  str  bool chr  int  dec  type obj
            ERR, ERR, ERR, ERR, INT, ERR, ERR, ERR,
        ERR, ERR, ERR, ERR, LIN, ERR, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[][] Operators = [
            Increment,
        Increment,
        Increment,
        Increment,
        NumericalIdentity,
        NumericalNegation,
        BooleanNegation,
        BitwiseCompliment
        ];

        internal static UnaryOperatorKind OpKind(UnaryOperatorKind kind, TypeSymbol operand) {
            var index = operand.TypeToIndex();

            if (index < 0)
                return UnaryOperatorKind.Error;

            var kindIndex = kind.OperatorIndex();
            var result = (kindIndex >= Operators.Length) ? UnaryOperatorKind.Error : Operators[kindIndex][index];

            return result == UnaryOperatorKind.Error ? result : result | kind;
        }
    }

    private void UnaryOperatorEasyOut(
        UnaryOperatorKind kind,
        BoundExpression operand,
        UnaryOperatorOverloadResolutionResult result) {
        var operandType = operand.Type();

        if (operandType is null)
            return;

        var easyOut = UnOpEasyOut.OpKind(kind, operandType);

        if (easyOut == UnaryOperatorKind.Error)
            return;

        var signature = OperatorFacts.GetSignature(easyOut);
        var conversion = Conversions.FastClassifyConversion(operandType, signature.operandType);

        result.results.Add(UnaryOperatorAnalysisResult.Applicable(signature, conversion));
    }
}
