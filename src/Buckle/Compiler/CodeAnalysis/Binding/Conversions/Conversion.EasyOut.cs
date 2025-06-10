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
                /*  any */{ IDN, UNB, UNB, UNB, UNB, UNB, UNB, NON, NUL, NUL, NUL, NUL, NUL, NUL, NUL, NON },
                /*  str */{ BOX, IDN, XPL, NON, XPL, XPL, NON, NON, NUL, NUL, NUL, NON, NUL, NUL, NON, NON },
                /* bool */{ BOX, NON, IDN, NON, NON, NON, NON, NON, NUL, NON, NUL, NON, NON, NON, NON, NON },
                /*  chr */{ BOX, NON, NON, IDN, NON, NON, NON, NON, NUL, NON, NON, NUL, NON, NON, NON, NON },
                /*  int */{ BOX, XPL, NON, NON, IDN, IPL, NON, NON, NUL, NUL, NON, NON, NUL, NUL, NON, NON },
                /*  dec */{ BOX, XPL, NON, NON, XPL, IDN, NON, NON, NUL, NUL, NON, NON, NUL, NUL, NON, NON },
                /* type */{ BOX, NON, NON, NON, NON, NON, IDN, NON, NUL, NON, NON, NON, NON, NON, NUL, NON },
                /*  obj */{ NON, NON, NON, NON, NON, NON, NON, IDN, NON, NON, NON, NON, NON, NON, NON, NUL },
                /* any? */{ XNL, XNL, XNL, XNL, XNL, XNL, XNL, NON, IDN, UNB, UNB, UNB, UNB, UNB, UNB, NON },
                /* str? */{ XNL, XNL, XNL, NON, XNL, XNL, NON, NON, NUL, IDN, NUL, NON, NUL, NUL, NON, NON },
                /*bool? */{ XNL, NON, XNL, NON, NON, NON, NON, NON, NUL, NON, IDN, NON, NON, NON, NON, NON },
                /* chr? */{ XNL, NON, NON, XNL, NON, NON, NON, NON, NUL, NON, NON, IDN, NON, NON, NON, NON },
                /* int? */{ XNL, XNL, NON, NON, XNL, XNL, NON, NON, NUL, NUL, NON, NON, IDN, NUL, NON, NON },
                /* dec? */{ XNL, XNL, NON, NON, XNL, XNL, NON, NON, NUL, NUL, NON, NON, NUL, IDN, NON, NON },
                /*type? */{ XNL, NON, NON, NON, NON, NON, XNL, NON, NUL, NON, NON, NON, NON, NON, IDN, NON },
                /* obj? */{ NON, NON, NON, NON, NON, NON, NON, XNL, NON, NON, NON, NON, NON, NON, NON, IDN }
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
