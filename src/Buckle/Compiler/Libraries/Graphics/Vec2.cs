using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static ClassSymbol Vec2 = Class("Vec2", [
        Field("x", BoundType.Decimal),
        Field("y", BoundType.Decimal),
        Constructor([
            ("x", BoundType.Decimal),
            ("y", BoundType.Decimal)
        ])
    ]);
}
