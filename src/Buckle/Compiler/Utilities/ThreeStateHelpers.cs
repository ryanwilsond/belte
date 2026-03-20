
namespace Buckle.Utilities;

internal static class ThreeStateHelpers {
    internal static ThreeState ToThreeState(this bool value) {
        return value ? ThreeState.True : ThreeState.False;
    }

    internal static bool HasValue(this ThreeState value) {
        return value != ThreeState.Unknown;
    }

    internal static bool Value(this ThreeState value) {
        return value == ThreeState.True;
    }
}
