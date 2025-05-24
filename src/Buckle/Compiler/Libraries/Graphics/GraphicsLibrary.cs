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
            StaticMethod("LoadTexture", SpecialType.Texture, true, [("path", SpecialType.String)]),
            StaticMethod("LoadSprite", SpecialType.Sprite, [("path", SpecialType.String, false), ("position", SpecialType.Vec2, true), ("scale", SpecialType.Vec2, true), ("rotation", SpecialType.Int, true)]),
            StaticMethod("DrawSprite", SpecialType.Int, true, [("sprite", SpecialType.Sprite, true)]),
            StaticMethod("LoadText", SpecialType.Text, true, [("text", SpecialType.String, true), ("fontPath", SpecialType.String, false), ("position", SpecialType.Vec2, true), ("fontSize", SpecialType.Decimal, false), ("angle", SpecialType.Decimal, true), ("r", SpecialType.Int, true), ("g", SpecialType.Int, true), ("b", SpecialType.Int, true)]),
            StaticMethod("DrawText", SpecialType.Int, true, [("sprite", SpecialType.Text, true)]),
            StaticMethod("DrawRect", SpecialType.Int, true, [("rect", SpecialType.Rect, true), ("r", SpecialType.Int, true), ("g", SpecialType.Int, true), ("b", SpecialType.Int, true)]),
            StaticMethod("StopDraw", SpecialType.Void, [("id", SpecialType.Int, true)]),
            StaticMethod("GetKey", SpecialType.Bool, [("key", SpecialType.String)]),
            StaticMethod("Fill", SpecialType.Void, [("r", SpecialType.Int), ("g", SpecialType.Int), ("b", SpecialType.Int)]),
        ]);
    }
}
