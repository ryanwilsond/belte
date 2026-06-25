using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis;
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

    internal static bool MethodProducesTemp(MethodSymbol method) {
        return method.name == "GetMousePosition" || method.name == "LoadSprite";
    }

    internal static IEnumerable<SynthesizedFinishedNamedTypeSymbol> GetTypes() {
        yield return Graphics;
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateGraphics() {
        return StaticClass("Graphics", [
            StaticMethod("Initialize", SpecialType.Void, [("title", SpecialType.String, false, null), ("width", SpecialType.Int, false, null), ("height", SpecialType.Int, false, null), ("usePointClamp", SpecialType.Bool, false, false)]),
            StaticMethod("LockFramerate", SpecialType.Void, [("fps", SpecialType.Int)]),
            StaticMethod("LoadTexture", WellKnownType.Texture, [("path", SpecialType.String)]),
            StaticMethod("LoadTexture", WellKnownType.Texture, [("path", SpecialType.String), ("r", SpecialType.Int), ("g", SpecialType.Int), ("b", SpecialType.Int)]),
            StaticMethod("LoadSprite", WellKnownType.Sprite, [("path", SpecialType.String, false), ("position", WellKnownType.Vec2, false), ("scale", WellKnownType.Vec2, true), ("rotation", SpecialType.Int, true)]),
            StaticMethod("Draw", SpecialType.Int, true, [("texture", WellKnownType.Texture, false), ("srcRect", WellKnownType.Rect, false), ("dstRect", WellKnownType.Rect, false), ("rotation", SpecialType.Int, true), ("flip", SpecialType.Bool, true), ("alpha", SpecialType.Decimal, true)]),
            StaticMethod("DrawSprite", SpecialType.Int, true, [("sprite", WellKnownType.Sprite)]),
            StaticMethod("DrawSprite", SpecialType.Int, true, [("sprite", WellKnownType.Sprite), ("offset", WellKnownType.Vec2)]),
            StaticMethod("LoadText", WellKnownType.Text, [("text", SpecialType.String, false), ("fontPath", SpecialType.String, false), ("position", WellKnownType.Vec2, false), ("fontSize", SpecialType.Decimal, false), ("angle", SpecialType.Decimal, true), ("r", SpecialType.Int, true), ("g", SpecialType.Int, true), ("b", SpecialType.Int, true)]),
            StaticMethod("DrawText", SpecialType.Int, true, [("sprite", WellKnownType.Text)]),
            StaticMethod("DrawRect", SpecialType.Int, true, [("rect", WellKnownType.Rect), ("r", SpecialType.Int), ("g", SpecialType.Int), ("b", SpecialType.Int)]),
            StaticMethod("DrawRect", SpecialType.Int, true, [("rect", WellKnownType.Rect), ("r", SpecialType.Int), ("g", SpecialType.Int), ("b", SpecialType.Int), ("a", SpecialType.Int)]),
            StaticMethod("StopDraw", SpecialType.Void, [("id", SpecialType.Int, true)]),
            StaticMethod("GetKey", SpecialType.Bool, [("key", SpecialType.String)]),
            StaticMethod("Fill", SpecialType.Void, [("r", SpecialType.Int), ("g", SpecialType.Int), ("b", SpecialType.Int)]),
            StaticMethod("GetMouseButton", SpecialType.Bool, [("button", SpecialType.String)]),
            StaticMethod("GetScroll", SpecialType.Int),
            StaticMethod("GetMousePosition", WellKnownType.Vec2),
            StaticMethod("LoadSound", WellKnownType.Sound, [("path", SpecialType.String)]),
            StaticMethod("PlaySound", SpecialType.Void, [("sound", WellKnownType.Sound)]),
            StaticMethod("SetCursorVisibility", SpecialType.Void, [("visible", SpecialType.Bool)]),
        ]);
    }
}
