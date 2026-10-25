
namespace Buckle.CodeAnalysis;

/// <summary>
/// Well known types.
/// The difference between these and SpecialType is that the compiler defines the semantics of SpecialTypes, while
/// these types are only used (not defined).
/// </summary>
internal enum WellKnownType : byte {
    None,

    First = SpecialType.NextAvailable,

    Belte_Enumerator = First,

    Belte_ValueTuple_T1,
    Belte_ValueTuple_T2,
    Belte_ValueTuple_T3,
    Belte_ValueTuple_T4,
    Belte_ValueTuple_T5,
    Belte_ValueTuple_T6,
    Belte_ValueTuple_T7,
    Belte_ValueTuple_TRest,

    System_Exception,
    System_Collections_IEnumerable,
    System_Collections_Generic_IEnumerable_T,
    System_Collections_IEnumerator,
    System_Collections_Generic_IEnumerator_T,
    System_Attribute,
    System_AttributeUsageAttribute,
    System_String,
    System_Activator,
    System_InvalidOperationException,
    System_Type,

    Belte_NoAllocAttribute,
    Belte_NoThrowAttribute,
    Belte_PureAttribute,
    Belte_CompilerServices_BelteMetadataAttribute,
    Belte_NullabilityAttribute,
    Belte_ConstMethodAttribute,
    Belte_ConstParamAttribute,
    Belte_ConstExprParamAttribute,

    Belte_List,
    Belte_Dictionary,
    Belte_Result,
    Belte_WrappedErrorException,

    Belte_Graphics_Vec2,
    Belte_Graphics_Sprite,
    Belte_Graphics_Text,
    Belte_Graphics_Rect,
    Belte_Graphics_Texture,
    Belte_Graphics_Sound,

    Buckle_CodeAnalysis_Text_TextLocation,


    NextAvailable,

    ExtSentinel = 255,
}
