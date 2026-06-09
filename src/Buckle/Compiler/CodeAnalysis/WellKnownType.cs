
namespace Buckle.CodeAnalysis;

/// <summary>
/// Well known types.
/// The difference between these and SpecialType is that the compiler defines the semantics of SpecialTypes, while
/// these types are only used (not defined).
/// </summary>
internal enum WellKnownType : byte {
    None,

    // Required

    Enumerator,
    Exception,

    // Non-required

    List,
    Dictionary,

    ValueTuple_T1,
    ValueTuple_T2,
    ValueTuple_T3,
    ValueTuple_T4,
    ValueTuple_T5,
    ValueTuple_T6,
    ValueTuple_T7,
    ValueTuple_TRest,

    Vec2,
    Sprite,
    Text,
    Rect,
    Texture,
    Sound,

    Array,
}
