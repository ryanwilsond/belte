using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis;

internal static class WellKnownMembers {
    private static readonly ImmutableArray<MemberDescriptor> Descriptors;

    static WellKnownMembers() {
        var initializationBytes = new byte[] {
                // Nullable_ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)SpecialType.Nullable,                                                                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,

                // Nullable_getValue
                (byte)MemberFlags.PropertyGet,                                                                              // Flags
                (byte)SpecialType.Nullable,                                                                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,            // Return Type

                // Nullable_getHasValue
                (byte)MemberFlags.PropertyGet,                                                                              // Flags
                (byte)SpecialType.Nullable,                                                                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Bool, // Return Type

                // Nullable_GetValueOrDefault
                (byte)MemberFlags.Method,                                                                                   // Flags
                (byte)SpecialType.Nullable,                                                                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,            // Return Type

                // Nullable_GetValueOrDefault_T
                (byte)MemberFlags.Method,                                                                                   // Flags
                (byte)SpecialType.Nullable,                                                                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,            // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,


                // ValueTuple_T1__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T1,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T2__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T2,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T2__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T2,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T3__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T3,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T3__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T3,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T3__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T3,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_T4__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T4,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T4__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T4,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T4__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T4,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_T4__Item4
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T4,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,                                                        // Field Signature

                // ValueTuple_T5__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T5__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T5__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_T5__Item4
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,                                                        // Field Signature

                // ValueTuple_T5__Item5
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,                                                        // Field Signature

                // ValueTuple_T6__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T6__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T6__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_T6__Item4
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,                                                        // Field Signature

                // ValueTuple_T6__Item5
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,                                                        // Field Signature

                // ValueTuple_T6__Item6
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,                                                        // Field Signature

                // ValueTuple_T7__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_T7__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_T7__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_T7__Item4
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,                                                        // Field Signature

                // ValueTuple_T7__Item5
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,                                                        // Field Signature

                // ValueTuple_T7__Item6
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,                                                        // Field Signature

                // ValueTuple_T7__Item7
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 6,                                                        // Field Signature

                // ValueTuple_TRest__Item1
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // ValueTuple_TRest__Item2
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,                                                        // Field Signature

                // ValueTuple_TRest__Item3
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,                                                        // Field Signature

                // ValueTuple_TRest__Item4
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,                                                        // Field Signature

                // ValueTuple_TRest__Item5
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,                                                        // Field Signature

                // ValueTuple_TRest__Item6
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,                                                        // Field Signature

                // ValueTuple_TRest__Item7
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 6,                                                        // Field Signature

                // ValueTuple_TRest__Rest
                (byte)MemberFlags.Field,                                                                                    // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (byte)SignatureTypeCode.GenericTypeParameter, 7,                                                        // Field Signature

                // ValueTuple_T1__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T1,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,

                // ValueTuple_T2__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T2,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,

                // ValueTuple_T3__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T3,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,

                 // ValueTuple_T4__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T4,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,


                // ValueTuple_T5__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T5,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,

                // ValueTuple_T6__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T6,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    6,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,

                // ValueTuple_T7__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_T7,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    7,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,
                    (byte)SignatureTypeCode.GenericTypeParameter, 6,

                // ValueTuple_TRest__ctor
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.ValueTuple_TRest,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    8,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,
                    (byte)SignatureTypeCode.GenericTypeParameter, 1,
                    (byte)SignatureTypeCode.GenericTypeParameter, 2,
                    (byte)SignatureTypeCode.GenericTypeParameter, 3,
                    (byte)SignatureTypeCode.GenericTypeParameter, 4,
                    (byte)SignatureTypeCode.GenericTypeParameter, 5,
                    (byte)SignatureTypeCode.GenericTypeParameter, 6,
                    (byte)SignatureTypeCode.GenericTypeParameter, 7,

                // Array_ctor_1
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.Array,                                                                                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Int,

                // Array_ctor_2
                (byte)MemberFlags.Constructor,                                                                              // Flags
                (byte)WellKnownType.Array,                                                                                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Void, // Return Type
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Int,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SignatureTypeCode.SZArray, (byte)SignatureTypeCode.GenericTypeParameter, 0,

                // Array_Get
                (byte)MemberFlags.Method,                                                                                   // Flags
                (byte)SpecialType.Array,                                                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.ByReference, (byte)SignatureTypeCode.GenericTypeParameter, 0, // Return Type

                // Array_Set
                (byte)MemberFlags.Method,                                                                                   // Flags
                (byte)SpecialType.Array,                                                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (byte)SignatureTypeCode.GenericTypeParameter, 0,            // Return Type
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.Int,
                    (byte)SignatureTypeCode.GenericTypeParameter, 0
        };

        var allNames = new string[(int)WellKnownMember.Count] {
            ".ctor",                    // Nullable_ctor
            "get_Value",                // Nullable_getValue
            "get_HasValue",             // Nullable_getHasValue
            "GetValueOrDefault",        // Nullable_GetValueOrDefault
            "GetValueOrDefault",        // Nullable_GetValueOrDefault_T
            "Item1",                    // ValueTuple_T1_Item1
            "Item1",                    // ValueTuple_T2_Item1
            "Item2",                    // ValueTuple_T2_Item2
            "Item1",                    // ValueTuple_T3_Item1
            "Item2",                    // ValueTuple_T3_Item2
            "Item3",                    // ValueTuple_T3_Item3
            "Item1",                    // ValueTuple_T4_Item1,
            "Item2",                    // ValueTuple_T4_Item2,
            "Item3",                    // ValueTuple_T4_Item3,
            "Item4",                    // ValueTuple_T4_Item4,
            "Item1",                    // ValueTuple_T5_Item1,
            "Item2",                    // ValueTuple_T5_Item2,
            "Item3",                    // ValueTuple_T5_Item3,
            "Item4",                    // ValueTuple_T5_Item4,
            "Item5",                    // ValueTuple_T5_Item5,
            "Item1",                    // ValueTuple_T6_Item1,
            "Item2",                    // ValueTuple_T6_Item2,
            "Item3",                    // ValueTuple_T6_Item3,
            "Item4",                    // ValueTuple_T6_Item4,
            "Item5",                    // ValueTuple_T6_Item5,
            "Item6",                    // ValueTuple_T6_Item6,
            "Item1",                    // ValueTuple_T7_Item1,
            "Item2",                    // ValueTuple_T7_Item2,
            "Item3",                    // ValueTuple_T7_Item3,
            "Item4",                    // ValueTuple_T7_Item4,
            "Item5",                    // ValueTuple_T7_Item5,
            "Item6",                    // ValueTuple_T7_Item6,
            "Item7",                    // ValueTuple_T7_Item7,
            "Item1",                    // ValueTuple_TRest_Item1,
            "Item2",                    // ValueTuple_TRest_Item2,
            "Item3",                    // ValueTuple_TRest_Item3,
            "Item4",                    // ValueTuple_TRest_Item4,
            "Item5",                    // ValueTuple_TRest_Item5,
            "Item6",                    // ValueTuple_TRest_Item6,
            "Item7",                    // ValueTuple_TRest_Item7,
            "Rest",                     // ValueTuple_TRest_Rest,
            ".ctor",                    // ValueTuple_T1_ctor,
            ".ctor",                    // ValueTuple_T2_ctor,
            ".ctor",                    // ValueTuple_T3_ctor,
            ".ctor",                    // ValueTuple_T4_ctor,
            ".ctor",                    // ValueTuple_T5_ctor,
            ".ctor",                    // ValueTuple_T6_ctor,
            ".ctor",                    // ValueTuple_T7_ctor,
            ".ctor",                    // ValueTuple_TRest_ctor,
            ".ctor",                    // Array_ctor_1,
            ".ctor",                    // Array_ctor_2,
            "Get",                      // Array_Get,
            "Set",                      // Array_Set,
        };

        Descriptors = MemberDescriptor.InitializeFromStream(
            new System.IO.MemoryStream(initializationBytes, writable: false),
            allNames
        );

#if DEBUG
        foreach (var descriptor in Descriptors) {
            Debug.Assert(!descriptor.isSpecialTypeMember);
        }
#endif
    }

    internal static MemberDescriptor GetDescriptor(WellKnownMember member) {
        return Descriptors[(int)member];
    }
}
