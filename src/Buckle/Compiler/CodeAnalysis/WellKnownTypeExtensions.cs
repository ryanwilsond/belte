
namespace Buckle.CodeAnalysis;

internal static class WellKnownTypeExtensions {
    internal static bool ShouldEmit(this WellKnownType wellKnownType, bool includeGraphicsTypes) {
        switch (wellKnownType) {
            case WellKnownType.None:
            case WellKnownType.List:
            case WellKnownType.Dictionary:
            case WellKnownType.Enumerator:
                return true;
            case WellKnownType.Vec2 when includeGraphicsTypes:
            case WellKnownType.Sprite when includeGraphicsTypes:
            case WellKnownType.Text when includeGraphicsTypes:
            case WellKnownType.Rect when includeGraphicsTypes:
            case WellKnownType.Texture when includeGraphicsTypes:
            case WellKnownType.Sound when includeGraphicsTypes:
                return true;
            case WellKnownType.Exception:
            case WellKnownType.ValueTuple_T1:
            case WellKnownType.ValueTuple_T2:
            case WellKnownType.ValueTuple_T3:
            case WellKnownType.ValueTuple_T4:
            case WellKnownType.ValueTuple_T5:
            case WellKnownType.ValueTuple_T6:
            case WellKnownType.ValueTuple_T7:
            case WellKnownType.ValueTuple_TRest:
            default:
                return false;
        }
    }
}
