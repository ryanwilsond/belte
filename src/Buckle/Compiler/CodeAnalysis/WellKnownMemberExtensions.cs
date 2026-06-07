using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class WellKnownMemberExtensions {
    private static readonly string[] MetadataNames = [
        WellKnownMemberNames.InstanceConstructorName,
        "get_Value",
        "get_HasValue",
        "GetValueOrDefault",

        "Item1",

        "Item1",
        "Item2",

        "Item1",
        "Item2",
        "Item3",

        "Item1",
        "Item2",
        "Item3",
        "Item4",

        "Item1",
        "Item2",
        "Item3",
        "Item4",
        "Item5",

        "Item1",
        "Item2",
        "Item3",
        "Item4",
        "Item5",
        "Item6",

        "Item1",
        "Item2",
        "Item3",
        "Item4",
        "Item5",
        "Item6",
        "Item7",

        "Item1",
        "Item2",
        "Item3",
        "Item4",
        "Item5",
        "Item6",
        "Item7",
        "Rest",

        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,
        WellKnownMemberNames.InstanceConstructorName,

        WellKnownMemberNames.InstanceConstructorName,
        "Get",
        "Set",
    ];

    internal static bool IsTupleMember(this WellKnownMember wellKnownMember) {
        return wellKnownMember >= WellKnownMember.ValueTuple_T1_Item1 &&
               wellKnownMember <= WellKnownMember.ValueTuple_TRest_ctor;
    }

    internal static string GetMetadataName(this WellKnownMember wellKnownMember) {
        return MetadataNames[(int)wellKnownMember - 1];
    }
}
