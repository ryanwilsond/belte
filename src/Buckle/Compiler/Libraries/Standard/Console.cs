using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Console = StaticClass("Console",
        [
    /* 0 */ StaticClass("Color", [
                Constexpr("Black", BoundType.Int, 0),
                Constexpr("DarkBlue", BoundType.Int, 1),
                Constexpr("DarkGreen", BoundType.Int, 2),
                Constexpr("DarkCyan", BoundType.Int, 3),
                Constexpr("DarkRed", BoundType.Int, 4),
                Constexpr("DarkMagenta", BoundType.Int, 5),
                Constexpr("DarkYellow", BoundType.Int, 6),
                Constexpr("Gray", BoundType.Int, 7),
                Constexpr("DarkGray", BoundType.Int, 8),
                Constexpr("Blue", BoundType.Int, 9),
                Constexpr("Green", BoundType.Int, 10),
                Constexpr("Cyan", BoundType.Int, 11),
                Constexpr("Red", BoundType.Int, 12),
                Constexpr("Magenta", BoundType.Int, 13),
                Constexpr("Yellow", BoundType.Int, 14),
                Constexpr("White", BoundType.Int, 15)
            ]),
    /* 1 */ StaticMethod("PrintLine", BoundType.Void, [
                        ("message", BoundType.NullableString)
            ]),
    /* 2 */ StaticMethod("PrintLine", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
    /* 3 */ StaticMethod("PrintLine", BoundType.Void, []),
    /* 4 */ StaticMethod("Print", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
    /* 5 */ StaticMethod("Print", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
    /* 6 */ StaticMethod("Input", BoundType.String, []),
    /* 7 */ StaticMethod("SetForegroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
    /* 8 */ StaticMethod("SetBackgroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
    /* 9 */ StaticMethod("ResetColor", BoundType.Void, []),
   /* 10 */ StaticMethod("GetWidth", BoundType.Int, []),
   /* 11 */ StaticMethod("GetHeight", BoundType.Int, []),
   /* 12 */ StaticMethod("SetCursorPosition", BoundType.Void, [
                ("left", BoundType.NullableInt),
                ("top", BoundType.NullableInt)
            ]),
        ]
    );
}
