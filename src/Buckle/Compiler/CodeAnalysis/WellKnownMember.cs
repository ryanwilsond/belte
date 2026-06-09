
namespace Buckle.CodeAnalysis;

internal enum WellKnownMember : byte {
    None,

    Nullable_ctor,
    Nullable_getValue,
    Nullable_getHasValue,
    Nullable_GetValueOrDefault,

    ValueTuple_T1_Item1,

    ValueTuple_T2_Item1,
    ValueTuple_T2_Item2,

    ValueTuple_T3_Item1,
    ValueTuple_T3_Item2,
    ValueTuple_T3_Item3,

    ValueTuple_T4_Item1,
    ValueTuple_T4_Item2,
    ValueTuple_T4_Item3,
    ValueTuple_T4_Item4,

    ValueTuple_T5_Item1,
    ValueTuple_T5_Item2,
    ValueTuple_T5_Item3,
    ValueTuple_T5_Item4,
    ValueTuple_T5_Item5,

    ValueTuple_T6_Item1,
    ValueTuple_T6_Item2,
    ValueTuple_T6_Item3,
    ValueTuple_T6_Item4,
    ValueTuple_T6_Item5,
    ValueTuple_T6_Item6,

    ValueTuple_T7_Item1,
    ValueTuple_T7_Item2,
    ValueTuple_T7_Item3,
    ValueTuple_T7_Item4,
    ValueTuple_T7_Item5,
    ValueTuple_T7_Item6,
    ValueTuple_T7_Item7,

    ValueTuple_TRest_Item1,
    ValueTuple_TRest_Item2,
    ValueTuple_TRest_Item3,
    ValueTuple_TRest_Item4,
    ValueTuple_TRest_Item5,
    ValueTuple_TRest_Item6,
    ValueTuple_TRest_Item7,
    ValueTuple_TRest_Rest,

    ValueTuple_T1_ctor,
    ValueTuple_T2_ctor,
    ValueTuple_T3_ctor,
    ValueTuple_T4_ctor,
    ValueTuple_T5_ctor,
    ValueTuple_T6_ctor,
    ValueTuple_T7_ctor,
    ValueTuple_TRest_ctor,

    Array_ctor_1,
    Array_ctor_2,
    Array_Get,
    Array_Set,
}
