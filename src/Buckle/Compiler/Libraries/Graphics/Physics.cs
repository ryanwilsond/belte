using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static ClassSymbol Physics = StaticClass("Physics", [
        StaticMethod("AxisAlignedCollision", BoundType.Bool, [
            ("sprite1", Nullable(Sprite)),
            ("sprite2", Nullable(Sprite))
        ])
    ]);
}
