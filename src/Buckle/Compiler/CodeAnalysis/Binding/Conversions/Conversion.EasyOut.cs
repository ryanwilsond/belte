using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class Conversion {
    private static class EasyOut {
        private static readonly byte[,] CastKindMap;

        static EasyOut() {
            const byte NON = (byte)ConversionKind.None;
            const byte IDN = (byte)ConversionKind.Identity;
            const byte IPL = (byte)ConversionKind.Implicit;
            const byte XPL = (byte)ConversionKind.Explicit;
            const byte BOX = (byte)ConversionKind.AnyBoxing;
            const byte BNU = (byte)ConversionKind.AnyBoxingImplicitNullable;
            const byte BXN = (byte)ConversionKind.AnyBoxingExplicitNullable;
            const byte UNB = (byte)ConversionKind.AnyUnboxing;
            const byte UIN = (byte)ConversionKind.AnyUnboxingImplicitNullable;
            const byte UXN = (byte)ConversionKind.AnyUnboxingExplicitNullable;
            const byte NUL = (byte)ConversionKind.ImplicitNullable;
            const byte XNL = (byte)ConversionKind.ExplicitNullable;

            CastKindMap = new byte[,] {
                // Casting Y to X:
                //          any  str  bool chr  int  dec  type any? str?bool? chr? int? dec? type?
                /*  any */{ IDN, UNB, UNB, UNB, UNB, UNB, UNB, NUL, UIN, UIN, UIN, UIN, UIN, UIN },
                /*  str */{ BOX, IDN, XPL, NON, XPL, XPL, NON, BNU, NUL, XNL, NON, XNL, XNL, NON },
                /* bool */{ BOX, NON, IDN, NON, NON, NON, NON, BNU, NON, NUL, NON, NON, NON, NON },
                /*  chr */{ BOX, NON, NON, IDN, NON, NON, NON, BNU, NON, NON, NUL, NON, NON, NON },
                /*  int */{ BOX, XPL, NON, NON, IDN, IPL, NON, BNU, XNL, NON, NON, NUL, NON, NON },
                /*  dec */{ BOX, XPL, NON, NON, XPL, IDN, NON, BNU, XNL, NON, NON, XNL, NUL, NON },
                /* type */{ BOX, NON, NON, NON, NON, NON, IDN, BNU, NON, NON, NON, NON, NON, NUL },
                /* any? */{ XNL, UXN, UXN, UXN, UXN, UXN, UXN, IDN, UNB, UNB, UNB, UNB, UNB, UNB },
                /* str? */{ BXN, XNL, XNL, NON, XNL, XNL, NON, BNU, IDN, XNL, NON, XNL, XNL, NON },
                /*bool? */{ BXN, NON, XNL, NON, NON, NON, NON, BNU, NON, IDN, NON, NON, NON, NON },
                /* chr? */{ BXN, NON, NON, XNL, NON, NON, NON, BNU, NON, NON, IDN, NON, NON, NON },
                /* int? */{ BXN, XNL, NON, NON, XNL, XNL, NON, BNU, XNL, NON, NON, IDN, NUL, NON },
                /* dec? */{ BXN, XNL, NON, NON, XNL, XNL, NON, BNU, XNL, NON, NON, XNL, IDN, NON },
                /*type? */{ BXN, NON, NON, NON, NON, NON, XNL, BNU, NON, NON, NON, NON, NON, IDN }
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
