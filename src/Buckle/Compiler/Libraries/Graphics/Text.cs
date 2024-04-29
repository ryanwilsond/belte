using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static ClassSymbol Text = Class("Text", [
        Field("fontPath", BoundType.NullableString),
        Field("text", BoundType.NullableString),
        Field("position", Nullable(Vec2)),
        Field("fontPt", BoundType.NullableInt),
        Field("r", BoundType.NullableInt),
        Field("g", BoundType.NullableInt),
        Field("b", BoundType.NullableInt)
    ]);
}
