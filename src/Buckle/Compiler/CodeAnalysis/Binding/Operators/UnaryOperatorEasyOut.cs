using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal static class UnOpEasyOut {
        private const UnaryOperatorKind ERR = UnaryOperatorKind.Error;
        private const UnaryOperatorKind INT = UnaryOperatorKind.Int;
        private const UnaryOperatorKind UNT = UnaryOperatorKind.UInt;
        private const UnaryOperatorKind F32 = UnaryOperatorKind.Float32;
        private const UnaryOperatorKind F64 = UnaryOperatorKind.Float64;
        private const UnaryOperatorKind BOL = UnaryOperatorKind.Bool;
        private const UnaryOperatorKind LIN = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int;
        private const UnaryOperatorKind LUN = UnaryOperatorKind.Lifted | UnaryOperatorKind.UInt;
        private const UnaryOperatorKind LF3 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Float32;
        private const UnaryOperatorKind LF6 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Float64;
        private const UnaryOperatorKind LBO = UnaryOperatorKind.Lifted | UnaryOperatorKind.Bool;

        private static readonly UnaryOperatorKind[] Increment = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj
            ERR, ERR, ERR, ERR, INT, F64, ERR, INT, INT, INT, INT, UNT, UNT, UNT, UNT, F32, F64, ERR,
            ERR, ERR, ERR, ERR, LIN, LF6, ERR, LIN, LIN, LIN, LIN, LUN, LUN, LUN, LUN, LF3, LF6, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalIdentity = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj
            ERR, ERR, ERR, ERR, INT, F64, ERR, INT, INT, INT, INT, UNT, UNT, UNT, UNT, F32, F64, ERR,
            ERR, ERR, ERR, ERR, LIN, LF6, ERR, LIN, LIN, LIN, LIN, LUN, LUN, LUN, LUN, LF3, LF6, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalNegation = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj
            ERR, ERR, ERR, ERR, INT, F64, ERR, INT, INT, INT, INT, ERR, ERR, ERR, ERR, F32, F64, ERR,
            ERR, ERR, ERR, ERR, LIN, LF6, ERR, LIN, LIN, LIN, LIN, ERR, ERR, ERR, ERR, LF3, LF6, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BooleanNegation = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj
            ERR, ERR, BOL, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR,
            ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BitwiseCompliment = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj
            ERR, ERR, ERR, ERR, INT, ERR, ERR, INT, INT, INT, INT, UNT, UNT, UNT, UNT, ERR, ERR, ERR,
            ERR, ERR, ERR, ERR, LIN, ERR, ERR, LIN, LIN, LIN, LIN, LUN, LUN, LUN, LUN, ERR, ERR, ERR // lifted
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

            index = EnlargeNumericType(index);

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
