using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static ClassSymbol Graphics = StaticClass("Graphics", [
        StaticMethod("Initialize", BoundType.Void, [
            ("title", BoundType.NullableString),
            ("screenWidth", BoundType.NullableInt),
            ("screenHeight", BoundType.NullableInt)
        ]),
        StaticMethod("LoadSprite", Nullable(Sprite), [
            ("path", BoundType.NullableString),
            ("position", Nullable(Vec2)),
            ("scale", Nullable(Vec2))
        ]),
        StaticMethod("LoadText", Nullable(Text), [
            ("fontPath", BoundType.NullableString),
            ("text", BoundType.NullableString),
            ("position", Nullable(Vec2)),
            ("fontPt", BoundType.NullableInt),
            ("r", BoundType.NullableInt),
            ("g", BoundType.NullableInt),
            ("b", BoundType.NullableInt)
        ]),
        StaticMethod("DrawSprite", BoundType.Void, [
            ("sprite", NullableRef(Sprite))
        ]),
        StaticMethod("DrawText", BoundType.Void, [
            ("text", NullableRef(Text))
        ]),
    ]);
}
