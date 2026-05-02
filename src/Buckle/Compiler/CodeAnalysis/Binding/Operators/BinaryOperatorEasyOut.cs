using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal static class BinOpEasyOut {
        private const BinaryOperatorKind ERR = BinaryOperatorKind.Error;
        private const BinaryOperatorKind ANY = BinaryOperatorKind.Any;
        private const BinaryOperatorKind OBJ = BinaryOperatorKind.Object;
        private const BinaryOperatorKind STR = BinaryOperatorKind.String;
        private const BinaryOperatorKind I32 = BinaryOperatorKind.Int32;
        private const BinaryOperatorKind I64 = BinaryOperatorKind.Int64;
        private const BinaryOperatorKind U32 = BinaryOperatorKind.UInt32;
        private const BinaryOperatorKind U64 = BinaryOperatorKind.UInt64;
        private const BinaryOperatorKind SIN = BinaryOperatorKind.Float32;
        private const BinaryOperatorKind DEC = BinaryOperatorKind.Float64;
        private const BinaryOperatorKind BOL = BinaryOperatorKind.Bool;
        private const BinaryOperatorKind CHR = BinaryOperatorKind.Char;
        private const BinaryOperatorKind TYP = BinaryOperatorKind.Type;
        private const BinaryOperatorKind LAN = BinaryOperatorKind.Lifted | BinaryOperatorKind.Any;
        private const BinaryOperatorKind LOB = BinaryOperatorKind.Lifted | BinaryOperatorKind.Object;
        private const BinaryOperatorKind LST = BinaryOperatorKind.Lifted | BinaryOperatorKind.String;
        private const BinaryOperatorKind LI3 = BinaryOperatorKind.Lifted | BinaryOperatorKind.Int32;
        private const BinaryOperatorKind LI6 = BinaryOperatorKind.Lifted | BinaryOperatorKind.Int64;
        private const BinaryOperatorKind LU3 = BinaryOperatorKind.Lifted | BinaryOperatorKind.UInt32;
        private const BinaryOperatorKind LU6 = BinaryOperatorKind.Lifted | BinaryOperatorKind.UInt64;
        private const BinaryOperatorKind LSI = BinaryOperatorKind.Lifted | BinaryOperatorKind.Float32;
        private const BinaryOperatorKind LDE = BinaryOperatorKind.Lifted | BinaryOperatorKind.Float64;
        private const BinaryOperatorKind LBO = BinaryOperatorKind.Lifted | BinaryOperatorKind.Bool;
        private const BinaryOperatorKind LCH = BinaryOperatorKind.Lifted | BinaryOperatorKind.Char;
        private const BinaryOperatorKind LTY = BinaryOperatorKind.Lifted | BinaryOperatorKind.Type;

        private static readonly BinaryOperatorKind[,] Arithmetic = {
            // Y <op> X:
            //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
            /*  any */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  str */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* bool */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  chr */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  int */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  dec */{ ERR, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* type */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  i08 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i16 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i32 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i64 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  u08 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u16 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u32 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u64 */{ ERR, ERR, ERR, ERR, ERR, DEC, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, LSI, LDE, ERR, ERR },
            /*  f32 */{ ERR, ERR, ERR, ERR, SIN, DEC, ERR, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /*  f64 */{ ERR, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*  obj */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* wnbl */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* any? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* str? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*bool? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* chr? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* int? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* dec? */{ ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*type? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* i08? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i16? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i32? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i64? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* u08? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u16? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u32? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u64? */{ ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR },
            /* f32? */{ ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /* f64? */{ ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* obj? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*wnbl? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR }
        };

        private static readonly BinaryOperatorKind[,] Addition = {
            // Y + X:
            //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
            /*  any */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  str */{ ERR, STR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* bool */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  chr */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  int */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  dec */{ ERR, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* type */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  i08 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i16 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i32 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i64 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  u08 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u16 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u32 */{ ERR, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u64 */{ ERR, ERR, ERR, ERR, ERR, DEC, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, LSI, LDE, ERR, ERR },
            /*  f32 */{ ERR, ERR, ERR, ERR, SIN, DEC, ERR, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /*  f64 */{ ERR, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*  obj */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* wnbl */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* any? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* str? */{ ERR, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*bool? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* chr? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* int? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* dec? */{ ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*type? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* i08? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i16? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i32? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i64? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* u08? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u16? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u32? */{ ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u64? */{ ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR },
            /* f32? */{ ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /* f64? */{ ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, ERR, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* obj? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*wnbl? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR }
        };

        private static readonly BinaryOperatorKind[,] Shift = {
            // Y <op> X:
            //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
            /*  any */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  str */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* bool */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  chr */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  int */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  dec */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* type */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  i08 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i16 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i32 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i64 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  u08 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u16 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u32 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u64 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, ERR, ERR, ERR, ERR },
            /*  f32 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  f64 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  obj */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* wnbl */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* any? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* str? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*bool? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* chr? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* int? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /* dec? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*type? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* i08? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i16? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i32? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i64? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /* u08? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u16? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u32? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u64? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, ERR, ERR, ERR, ERR },
            /* f32? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* f64? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* obj? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*wnbl? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR }
        };

        private static readonly BinaryOperatorKind[,] Equality = {
            // Y <op> X:
            //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
            /*  any */{ ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, ANY, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN },
            /*  str */{ ANY, STR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* bool */{ ANY, ERR, BOL, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  chr */{ ANY, ERR, ERR, CHR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, ERR, LCH, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  int */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  dec */{ ANY, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* type */{ ANY, ERR, ERR, ERR, ERR, ERR, TYP, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, ERR, ERR, ERR, ERR, LTY, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  i08 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i16 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i32 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  i64 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /*  u08 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u16 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I32, I32, I32, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u32 */{ ANY, ERR, ERR, ERR, I64, DEC, ERR, I64, I64, I64, I64, U32, U32, U32, U64, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, U32, U32, U32, U64, LSI, LDE, ERR, ERR },
            /*  u64 */{ ANY, ERR, ERR, ERR, ERR, DEC, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, LSI, LDE, ERR, ERR },
            /*  f32 */{ ANY, ERR, ERR, ERR, SIN, DEC, ERR, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, SIN, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /*  f64 */{ ANY, ERR, ERR, ERR, DEC, DEC, ERR, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, DEC, ERR, ERR, LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*  obj */{ ANY, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, OBJ, ERR, LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LOB, ERR },
            /* wnbl */{ ANY, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, I32, LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* any? */{ LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN, LAN },
            /* str? */{ LAN, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, LST, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*bool? */{ LAN, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* chr? */{ LAN, ERR, ERR, LCH, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, ERR, LCH, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* int? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* dec? */{ LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /*type? */{ LAN, ERR, ERR, ERR, ERR, ERR, LTY, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, ERR, ERR, ERR, ERR, LTY, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* i08? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i16? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i32? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, LSI, LDE, ERR, ERR },
            /* i64? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, LSI, LDE, ERR, ERR },
            /* u08? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u16? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u32? */{ LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LI6, LDE, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, LSI, LDE, ERR, ERR },
            /* u64? */{ LAN, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, ERR, LDE, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, LSI, LDE, ERR, ERR },
            /* f32? */{ LAN, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LSI, LDE, ERR, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LSI, LDE, ERR, ERR },
            /* f64? */{ LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR, LAN, ERR, ERR, ERR, LDE, LDE, ERR, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, LDE, ERR, ERR },
            /* obj? */{ LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LOB, ERR, LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LOB, ERR },
            /*wnbl? */{ LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LAN, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI3 }
        };

        private static readonly BinaryOperatorKind[,] Logical = {
            // Y <op> X:
            //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj  wnbl any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
            /*  any */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  str */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* bool */{ ERR, ERR, BOL, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  chr */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  int */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  dec */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* type */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  i08 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i16 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i32 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, I32, I32, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  i64 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, I64, I64, I64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /*  u08 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u16 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I32, I32, I32, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u32 */{ ERR, ERR, ERR, ERR, I64, ERR, ERR, I64, I64, I64, I64, U32, U32, U32, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, U32, U32, U32, U64, ERR, ERR, ERR, ERR },
            /*  u64 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, U64, U64, U64, U64, ERR, ERR, ERR, ERR },
            /*  f32 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  f64 */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*  obj */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* wnbl */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* any? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* str? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*bool? */{ ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LBO, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* chr? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* int? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /* dec? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*type? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* i08? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i16? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i32? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LI3, LI3, LI6, ERR, ERR, ERR, ERR, ERR },
            /* i64? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LI6, LI6, LI6, ERR, ERR, ERR, ERR, ERR },
            /* u08? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u16? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI3, LI3, LI3, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u32? */{ ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LI6, ERR, ERR, LI6, LI6, LI6, LI6, LU3, LU3, LU3, LU6, ERR, ERR, ERR, ERR },
            /* u64? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, LU6, LU6, LU6, LU6, ERR, ERR, ERR, ERR },
            /* f32? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* f64? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /* obj? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR },
            /*wnbl? */{ ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR, ERR }
        };

        private static readonly BinaryOperatorKind[][,] Operators = [
            Arithmetic,
            Addition,
            Arithmetic,
            Arithmetic,
            Arithmetic,
            Shift,
            Shift,
            Equality,
            Equality,
            Arithmetic,
            Arithmetic,
            Arithmetic,
            Arithmetic,
            Shift,
            Logical,
            Logical,
            Logical,
            Arithmetic
        ];

        internal static BinaryOperatorKind OpKind(BinaryOperatorKind kind, TypeSymbol left, TypeSymbol right) {
            var leftIndex = left.TypeToIndex();

            if (leftIndex < 0)
                return BinaryOperatorKind.Error;

            var rightIndex = right.TypeToIndex();

            if (rightIndex < 0)
                return BinaryOperatorKind.Error;

            var result = BinaryOperatorKind.Error;

            if (!kind.IsConditional() ||
                (leftIndex == (int)BinaryOperatorKind.Bool && rightIndex == (int)BinaryOperatorKind.Bool)) {
                result = Operators[kind.OperatorIndex()][leftIndex, rightIndex];
            }

            return result == BinaryOperatorKind.Error ? result : result | kind;
        }
    }

    private void BinaryOperatorEasyOut(
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BinaryOperatorOverloadResolutionResult result) {
        var leftType = left.Type();

        if (leftType is null)
            return;

        var rightType = right.Type();

        if (rightType is null)
            return;

        if (left.kind == BoundKind.LiteralExpression && right.kind != BoundKind.LiteralExpression &&
            !leftType.specialType.IsFloatingPoint()) {
            leftType = Binder.ReduceNumericIfApplicable(rightType, left).type;
        }

        if (right.kind == BoundKind.LiteralExpression && left.kind != BoundKind.LiteralExpression &&
            !rightType.specialType.IsFloatingPoint()) {
            rightType = Binder.ReduceNumericIfApplicable(leftType, right).type;
        }

        var easyOut = BinOpEasyOut.OpKind(kind, leftType, rightType);

        if (easyOut == BinaryOperatorKind.Error)
            return;

        var signature = OperatorFacts.GetSignature(easyOut);
        var leftConversion = Conversions.FastClassifyConversion(leftType, signature.leftType);
        var rightConversion = Conversions.FastClassifyConversion(rightType, signature.rightType);

        result.results.Add(BinaryOperatorAnalysisResult.Applicable(signature, leftConversion, rightConversion));
    }
}
