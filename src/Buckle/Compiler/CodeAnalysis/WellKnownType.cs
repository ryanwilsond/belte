
namespace Buckle.CodeAnalysis;

/// <summary>
/// Well known types.
/// The difference between these and SpecialType is that the compiler defines the semantics of SpecialTypes, while
/// these types are only used (not defined).
/// </summary>
internal enum WellKnownType : byte {
    None,

    First = SpecialType.NextAvailable,

    // Required

    Enumerator = First,

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

    DllImportAttribute,
    UnmanagedAttribute,
    MustUseReturnValueAttribute,

    // PE

    System_Exception,
    System_Collections_IEnumerable,
    System_Collections_Generic_IEnumerable_T,
    System_Collections_IEnumerator,
    System_Collections_Generic_IEnumerator_T,
    System_Attribute,
    System_AttributeUsageAttribute,

    Belte_NoAllocAttribute,
    Belte_NoThrowAttribute,
    Belte_PureAttribute,
    Belte_CompilerServices_BelteMetadataAttribute,
    Belte_NullabilityAttribute,


    LastNativeType = MustUseReturnValueAttribute,
    LastNativeRequiredType = Enumerator,
    FirstPEType = System_Exception,
    LastPEType = Belte_NullabilityAttribute,

    ExtSentinel = 255,
}
