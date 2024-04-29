using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static ClassSymbol Sprite = Class("Sprite", [
        Field("path", BoundType.NullableString),
        Field("position", Nullable(Vec2)),
        Field("scale", Nullable(Vec2))
    ]);
}
