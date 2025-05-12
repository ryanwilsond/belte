using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Libraries.LibraryHelpers;

namespace Buckle.Libraries;

internal static class GraphicsLibrary {
    private static SynthesizedFinishedNamedTypeSymbol _lazyGraphics;

    internal static SynthesizedFinishedNamedTypeSymbol Graphics {
        get {
            if (_lazyGraphics is null)
                Interlocked.CompareExchange(ref _lazyGraphics, GenerateGraphics(), null);

            return _lazyGraphics;
        }
    }

    internal static IEnumerable<SynthesizedFinishedNamedTypeSymbol> GetTypes() {
        yield return Graphics;
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateGraphics() {
        return StaticClass("Graphics", [
            StaticMethod("Initialize", SpecialType.Void, [("title", SpecialType.String), ("width", SpecialType.Int), ("height", SpecialType.Int)]),
            StaticMethod("LoadSprite", SpecialType.Sprite, [("path", SpecialType.String), ("position", SpecialType.Vec2), ("scale", SpecialType.Vec2), ("rotation", SpecialType.Int)]),
            StaticMethod("DrawSprite", SpecialType.Void, [("sprite", SpecialType.Sprite, true)]),
        ]);
    }
}
