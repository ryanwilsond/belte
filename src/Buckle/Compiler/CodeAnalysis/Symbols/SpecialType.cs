
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Special type of symbol, if any.
/// </summary>
public enum SpecialType : byte {
    None,
    Object,
    Array,
    Any,
    String,
    Bool,
    Char,
    Int,
    Decimal,
    Type,
    Nullable,
    Void,
    List,
    Dictionary,
    Vec2,
    Sprite,
    Text,
    Rect,
    Texture,
    Sound,
}
