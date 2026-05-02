using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    internal static class EasyOut {
        private static readonly byte[,] CastKindMap;

        static EasyOut() {
            const byte NON = (byte)ConversionKind.None;
            const byte IDN = (byte)ConversionKind.Identity;
            const byte IPL = (byte)ConversionKind.Implicit;
            const byte XPL = (byte)ConversionKind.Explicit;
            const byte BOX = (byte)ConversionKind.AnyBoxing;
            const byte UNB = (byte)ConversionKind.AnyUnboxing;
            const byte NUL = (byte)ConversionKind.ImplicitNullable;
            const byte XNL = (byte)ConversionKind.ExplicitNullable;
            const byte NUM = (byte)ConversionKind.ImplicitNumeric;
            const byte XNM = (byte)ConversionKind.ExplicitNumeric;

            CastKindMap = new byte[,] {
                // Casting Y to X:
                //          any  str  bool chr  int  dec  type i08  i16  i32  i64  u08  u16  u32  u64  f32  f64  obj wnbl  any? str?bool? chr? int? dec? type?i08? i16? i32? i64? u08? u16? u32? u64? f32? f64? obj?wnbl?
                /*  any */{ IDN, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, NON, UNB, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NUL },
                /*  str */{ BOX, IDN, XPL, NON, XPL, XPL, NON, XPL, XPL, XPL, XPL, XPL, XPL, XPL, XPL, XPL, XPL, NON, NON, NUL, NUL, NUL, NON, NUL, NUL, NON, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /* bool */{ BOX, XPL, IDN, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, IPL, NUL, XNL, NUL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL },
                /*  chr */{ BOX, IPL, NON, IDN, NUM, NUM, NON, XNM, XNM, NUM, NUM, XNM, NUM, NUM, NUM, NUM, NUM, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, XNL, NUL, NUL, XNL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /*  int */{ BOX, XPL, NON, XNM, IDN, NUM, NON, XNM, XNM, XNM, IDN, XNM, XNM, XNM, XNM, NUM, NUM, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, XNL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  dec */{ BOX, XPL, NON, XNM, XNM, IDN, NON, NUM, NUM, NUM, NUM, NUM, NUM, NUM, NUM, NUM, IDN, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /* type */{ BOX, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON },
                /*  i08 */{ BOX, XPL, NON, NUM, NUM, NUM, NON, IDN, NUM, NUM, NUM, XNM, XNM, XNM, XNM, NUM, NUM, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, NUL, NUL, NUL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  i16 */{ BOX, XPL, NON, NUM, NUM, NUM, NON, XNM, IDN, NUM, NUM, XNM, XNM, XNM, XNM, NUM, NUM, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, NUL, NUL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  i32 */{ BOX, XPL, NON, XNM, NUM, NUM, NON, XNM, XNM, IDN, NUM, XNM, XNM, XNM, XNM, NUM, NUM, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, NUL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  i64 */{ BOX, XPL, NON, XNM, IDN, NUM, NON, XNM, XNM, XNM, IDN, XNM, XNM, XNM, XNM, NUM, NUM, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, XNL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  u08 */{ BOX, XPL, NON, NUM, NUM, NUM, NON, XNM, NUM, NUM, NUM, IDN, NUM, NUM, NUM, NUM, NUM, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /*  u16 */{ BOX, XPL, NON, NUM, NUM, NUM, NON, XNM, XNM, NUM, NUM, XNM, IDN, NUM, NUM, NUM, NUM, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, XNL, NUL, NUL, XNL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /*  u32 */{ BOX, XPL, NON, XNM, NUM, NUM, NON, XNM, XNM, XNM, NUM, XNM, XNM, IDN, NUM, NUM, NUM, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, XNL, NUL, XNL, XNL, NUL, NUL, NUL, NUL, NON, NON },
                /*  u64 */{ BOX, XPL, NON, XNM, XNM, NUM, NON, XNM, XNM, XNM, XNM, XNM, XNM, XNM, IDN, NUM, NUM, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NUL, NUL, NUL, NON, NON },
                /*  f32 */{ BOX, XPL, NON, XNM, XNM, NUM, NON, XNM, XNM, XNM, XNM, XNM, XNM, XNM, XNM, IDN, NUM, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /*  f64 */{ BOX, XPL, NON, XNM, XNM, IDN, NON, XNM, XNM, XNM, XNM, XNM, XNM, XNM, XNM, XNM, IDN, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NUL, NON, NON },
                /*  obj */{ NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON },
                /* wnbl */{ BOX, NON, IPL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, IDN, NUL, NON, NUL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL },
                /* any? */{ XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, XNL, IDN, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, UNB, NON, UNB },
                /* str? */{ XNL, XNL, XNL, NON, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, IDN, NUL, NON, NUL, NUL, NON, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /*bool? */{ XNL, XNL, XNL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, XNL, NUL, XNL, IDN, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL },
                /* chr? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, IDN, NUL, NUL, NON, XNL, XNL, NUL, NUL, XNL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /* int? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, IDN, NUL, NON, XNL, XNL, XNL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /* dec? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL, NUL, NON, XNL, NUL, IDN, NON, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /*type? */{ XNL, NON, NON, NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON },
                /* i08? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, IDN, NUL, NUL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /* i16? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, IDN, NUL, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /* i32? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, IDN, NUL, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /* i64? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, XNL, IDN, XNL, XNL, XNL, XNL, NUL, NUL, NON, NON },
                /* u08? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, NUL, NUL, NUL, IDN, NUL, NUL, NUL, NUL, NUL, NON, NON },
                /* u16? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, NUL, NUL, NUL, NON, XNL, XNL, NUL, NUL, XNL, IDN, NUL, NUL, NUL, NUL, NON, NON },
                /* u32? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, NUL, NUL, NON, XNL, XNL, XNL, NUL, XNL, XNL, IDN, NUL, NUL, NUL, NON, NON },
                /* u64? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, IDN, NUL, NUL, NON, NON },
                /* f32? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, IDN, NUL, NON, NON },
                /* f64? */{ XNL, XNL, NON, XNL, XNL, XNL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, NON, NUL, NUL, NON, XNL, XNL, NUL, NON, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, XNL, IDN, NON, NON },
                /* obj? */{ NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, IDN, NON },
                /*wnbl? */{ XNL, NON, XNL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, XNL, NUL, NON, NUL, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, NON, IDN }
            };
        }

        internal static ConversionKind Classify(TypeSymbol source, TypeSymbol target) {
            if (source is null || target is null)
                return ConversionKind.None;

            var sourceIndex = source.TypeToIndex();

            if (sourceIndex < 0)
                return ConversionKind.None;

            var targetIndex = target.TypeToIndex();

            if (targetIndex < 0)
                return ConversionKind.None;

            return (ConversionKind)CastKindMap[sourceIndex, targetIndex];
        }
    }
}
