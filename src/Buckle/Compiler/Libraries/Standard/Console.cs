using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Console = StaticClass("Console",
        [
            StaticClass("Color", [
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
            StaticMethod("PrintLine", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
            StaticMethod("PrintLine", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
            StaticMethod("PrintLine", BoundType.Void, []),
            StaticMethod("Print", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
            StaticMethod("Print", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
            StaticMethod("Input", BoundType.Void, []),
            StaticMethod("SetForegroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
            StaticMethod("SetBackgroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
            StaticMethod("ResetColor", BoundType.Void, []),
        ]
    );
}
