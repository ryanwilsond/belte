using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal static class UnOpEasyOut {
        private const UnaryOperatorKind ERR = UnaryOperatorKind.Error;
        private const UnaryOperatorKind I08 = UnaryOperatorKind.Int8;
        private const UnaryOperatorKind I16 = UnaryOperatorKind.Int16;
        private const UnaryOperatorKind I32 = UnaryOperatorKind.Int32;
        private const UnaryOperatorKind I64 = UnaryOperatorKind.Int64;
        private const UnaryOperatorKind U08 = UnaryOperatorKind.UInt8;
        private const UnaryOperatorKind U16 = UnaryOperatorKind.UInt16;
        private const UnaryOperatorKind U32 = UnaryOperatorKind.UInt32;
        private const UnaryOperatorKind U64 = UnaryOperatorKind.UInt64;
        private const UnaryOperatorKind F32 = UnaryOperatorKind.Float32;
        private const UnaryOperatorKind F64 = UnaryOperatorKind.Float64;
        private const UnaryOperatorKind BOL = UnaryOperatorKind.Bool;
        private const UnaryOperatorKind LI08 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int8;
        private const UnaryOperatorKind LI16 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int16;
        private const UnaryOperatorKind LI32 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int32;
        private const UnaryOperatorKind LI64 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Int64;
        private const UnaryOperatorKind LU08 = UnaryOperatorKind.Lifted | UnaryOperatorKind.UInt8;
        private const UnaryOperatorKind LU16 = UnaryOperatorKind.Lifted | UnaryOperatorKind.UInt16;
        private const UnaryOperatorKind LU32 = UnaryOperatorKind.Lifted | UnaryOperatorKind.UInt32;
        private const UnaryOperatorKind LU64 = UnaryOperatorKind.Lifted | UnaryOperatorKind.UInt64;
        private const UnaryOperatorKind LF3 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Float32;
        private const UnaryOperatorKind LF6 = UnaryOperatorKind.Lifted | UnaryOperatorKind.Float64;
        private const UnaryOperatorKind LBO = UnaryOperatorKind.Lifted | UnaryOperatorKind.Bool;

        private static readonly UnaryOperatorKind[] Increment = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl
            ERR, ERR, ERR, ERR, I64, F64, ERR, I08, I16, I32, I64, U08, U16, U32, U64, F32, F64, ERR, ERR,
            ERR, ERR, ERR, ERR,LI64, LF6, ERR,LI08,LI16,LI32,LI64,LU08,LU16,LU32,LU64, LF3, LF6, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalIdentity = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl
            ERR, ERR, ERR, ERR, I64, F64, ERR, I32, I32, I32, I64, U32, U32, U32, U64, F32, F64, ERR, ERR,
            ERR, ERR, ERR, ERR,LI64, LF6, ERR,LI32,LI32,LI32,LI64,LU32,LU32,LU32,LU64, LF3, LF6, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] NumericalNegation = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl
            ERR, ERR, ERR, ERR, I64, F64, ERR, I32, I32, I32, I64, ERR, ERR, ERR, ERR, F32, F64, ERR, ERR,
            ERR, ERR, ERR, ERR,LI64, LF6, ERR,LI32,LI32,LI32,LI64, ERR, ERR, ERR, ERR, LF3, LF6, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BooleanNegation = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl
            ERR, ERR, BOL, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR,
            ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR // lifted
        ];

        private static readonly UnaryOperatorKind[] BitwiseCompliment = [
        //  any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl
            ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR,
            ERR, ERR, ERR, ERR,LI64, ERR, ERR,LI32,LI32,LI32,LI64,LU32,LU32,LU32,LU64, ERR, ERR, ERR, ERR // lifted
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
