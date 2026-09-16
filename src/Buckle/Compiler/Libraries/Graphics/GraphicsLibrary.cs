using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Libraries.LibraryHelpers;

namespace Buckle.Libraries;

internal class GraphicsLibrary {
    private SynthesizedFinishedNamedTypeSymbol _lazyGraphics;

    private readonly Compilation _compilation;

    internal GraphicsLibrary(Compilation compilation) {
        _compilation = compilation;
    }

    internal SynthesizedFinishedNamedTypeSymbol Graphics {
        get {
            if (_lazyGraphics is null)
                Interlocked.CompareExchange(ref _lazyGraphics, GenerateGraphics(), null);

            return _lazyGraphics;
        }
    }

    private SpecialOrKnownType Void => _compilation.GetSpecialType(SpecialType.Void);
    private SpecialOrKnownType String => _compilation.GetSpecialType(SpecialType.String);
    private SpecialOrKnownType Int => _compilation.GetSpecialType(SpecialType.Int);
    private SpecialOrKnownType Bool => _compilation.GetSpecialType(SpecialType.Bool);
    private SpecialOrKnownType Decimal => _compilation.GetSpecialType(SpecialType.Decimal);
    private SpecialOrKnownType Texture => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Texture);
    private SpecialOrKnownType Sprite => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Sprite);
    private SpecialOrKnownType Text => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Text);
    private SpecialOrKnownType Sound => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Sound);
    private SpecialOrKnownType Vec2 => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Vec2);
    private SpecialOrKnownType Rect => _compilation.GetWellKnownType(WellKnownType.Belte_Graphics_Rect);

    internal bool MethodProducesTemp(MethodSymbol method) {
        return method.name == "GetMousePosition" || method.name == "LoadSprite";
    }

    internal IEnumerable<SynthesizedFinishedNamedTypeSymbol> GetTypes() {
        yield return Graphics;
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateGraphics() {
        return StaticClass(_compilation, "Graphics", [
            StaticMethod("Initialize", Void, [("title", String, false, null), ("width", Int, false, null), ("height", Int, false, null), ("usePointClamp", Bool, false, false)]),
            StaticMethod("LockFramerate", Void, [("fps", Int)]),
            StaticMethod("LoadTexture", Texture, [("path", String)]),
            StaticMethod("LoadTexture", Texture, [("path", String), ("r", Int), ("g", Int), ("b", Int)]),
            StaticMethod("LoadSprite", Sprite, [("path", String, false), ("position", Vec2, false), ("scale", Vec2, true), ("rotation", Int, true)]),
            StaticMethod("Draw", Int, true, [("texture", Texture, false), ("srcRect", Rect, false), ("dstRect", Rect, false), ("rotation", Int, true), ("flip", Bool, true), ("alpha", Decimal, true)]),
            StaticMethod("DrawSprite", Int, true, [("sprite", Sprite)]),
            StaticMethod("DrawSprite", Int, true, [("sprite", Sprite), ("offset", Vec2)]),
            StaticMethod("LoadText", Text, [("text", String, false), ("fontPath", String, false), ("position", Vec2, false), ("fontSize", Decimal, false), ("angle", Decimal, true), ("r", Int, true), ("g", Int, true), ("b", Int, true)]),
            StaticMethod("DrawText", Int, true, [("sprite", Text)]),
            StaticMethod("DrawRect", Int, true, [("rect", Rect), ("r", Int), ("g", Int), ("b", Int)]),
            StaticMethod("DrawRect", Int, true, [("rect", Rect), ("r", Int), ("g", Int), ("b", Int), ("a", Int)]),
            StaticMethod("StopDraw", Void, [("id", Int, true)]),
            StaticMethod("GetKey", Bool, [("key", String)]),
            StaticMethod("Fill", Void, [("r", Int), ("g", Int), ("b", Int)]),
            StaticMethod("GetMouseButton", Bool, [("button", String)]),
            StaticMethod("GetScroll", Int),
            StaticMethod("GetMousePosition", Vec2),
            StaticMethod("LoadSound", Sound, [("path", String)]),
            StaticMethod("PlaySound", Void, [("sound", Sound)]),
            StaticMethod("SetCursorVisibility", Void, [("visible", Bool)]),
        ]);
    }
}
