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

            CastKindMap = new byte[,] {
                // Casting Y to X:
                //          any  str  bool chr  int  dec  type obj  any? str?bool? chr? int? dec? type?obj?
                /*  any */{ IDN, UNB, UNB, UNB, UNB, UNB, UNB, NON, NUL, NON, NON, NON, NON, NON, NON, NON },
                /*  str */{ BOX, IDN, XPL, NON, XPL, XPL, NON, NON, NON, NUL, XNL, NON, XNL, XNL, NON, NON },
                /* bool */{ BOX, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON, NON },
                /*  chr */{ BOX, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NUL, NON, NON, NON, NON },
                /*  int */{ BOX, XPL, NON, NON, IDN, IPL, NON, NON, NON, XNL, NON, NON, NUL, NON, NON, NON },
                /*  dec */{ BOX, XPL, NON, NON, XPL, IDN, NON, NON, NON, XNL, NON, NON, XNL, NUL, NON, NON },
                /* type */{ BOX, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NUL, NON },
                /*  obj */{ NON, NON, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NUL },
                /* any? */{ XNL, NON, NON, NON, NON, NON, NON, NON, IDN, UNB, UNB, UNB, UNB, UNB, UNB, NON },
                /* str? */{ NON, XNL, XNL, NON, XNL, XNL, NON, NON, NON, IDN, XNL, NON, XNL, XNL, NON, NON },
                /*bool? */{ NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON },
                /* chr? */{ NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON },
                /* int? */{ NON, XNL, NON, NON, XNL, XNL, NON, NON, NON, XNL, NON, NON, IDN, NUL, NON, NON },
                /* dec? */{ NON, XNL, NON, NON, XNL, XNL, NON, NON, NON, XNL, NON, NON, XNL, IDN, NON, NON },
                /*type? */{ NON, NON, NON, NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, IDN, NON },
                /* obj? */{ NON, NON, NON, NON, NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, IDN }
            };
        }

        internal static ConversionKind Classify(TypeSymbol source, TypeSymbol target) {
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
