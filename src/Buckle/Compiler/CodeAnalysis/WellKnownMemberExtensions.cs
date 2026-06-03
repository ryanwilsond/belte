
namespace Buckle.CodeAnalysis;

internal static class WellKnownMemberExtensions {
    internal static bool IsTupleMember(this WellKnownMember wellKnownMember) {
        return wellKnownMember >= WellKnownMember.ValueTuple_T1_Item1 &&
               wellKnownMember <= WellKnownMember.ValueTuple_TRest_ctor;
    }
}
