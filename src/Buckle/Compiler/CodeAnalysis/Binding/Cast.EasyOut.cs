using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class Cast {
    private static class EasyOut {
        private static readonly byte[,] CastKindMap;

        static EasyOut() {
            const byte NON = (byte)CastKind.None;
            const byte IDN = (byte)CastKind.Identity;
            const byte IPL = (byte)CastKind.Implicit;
            const byte XPL = (byte)CastKind.Explicit;
            const byte BOX = (byte)CastKind.AnyBoxing;
            const byte BNU = (byte)CastKind.AnyBoxingImplicitNullable;
            const byte BXN = (byte)CastKind.AnyBoxingExplicitNullable;
            const byte UNB = (byte)CastKind.AnyUnboxing;
            const byte UIN = (byte)CastKind.AnyUnboxingImplicitNullable;
            const byte UXN = (byte)CastKind.AnyUnboxingExplicitNullable;
            const byte NUL = (byte)CastKind.ImplicitNullable;
            const byte XNL = (byte)CastKind.ExplicitNullable;

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

        internal static CastKind Classify(TypeSymbol source, TypeSymbol target) {
            var sourceIndex = source.TypeToIndex();

            if (sourceIndex < 0)
                return CastKind.None;

            var targetIndex = target.TypeToIndex();

            if (targetIndex < 0)
                return CastKind.None;

            return (CastKind)CastKindMap[sourceIndex, targetIndex];
        }
    }
}
