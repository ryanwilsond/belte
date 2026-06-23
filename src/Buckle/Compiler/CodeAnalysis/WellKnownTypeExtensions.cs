
namespace Buckle.CodeAnalysis;

internal static class WellKnownTypeExtensions {
    private static readonly string[] MetadataNames = [
        "Enumerator`1",
        "Exception",
        "List`1",
        "Dictionary`2",
        "ValueTuple`1",
        "ValueTuple`2",
        "ValueTuple`3",
        "ValueTuple`4",
        "ValueTuple`5",
        "ValueTuple`6",
        "ValueTuple`7",
        "ValueTuple`8",
        "Vec2",
        "Sprite",
        "Text",
        "Rect",
        "Texture",
        "Sound",
        "Array`1",
        "Attribute",
        "DllImportAttribute",
        "UnmanagedAttribute",
    ];

    internal static bool ShouldEmit(this WellKnownType wellKnownType, bool includeGraphicsTypes) {
        switch (wellKnownType) {
            case WellKnownType.None:
            case WellKnownType.List:
            case WellKnownType.Dictionary:
            case WellKnownType.Enumerator:
            case WellKnownType.Array:
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
            case WellKnownType.Attribute:
            case WellKnownType.UnmanagedAttribute:
            case WellKnownType.DllImportAttribute:
            default:
                return false;
        }
    }

    internal static string GetMetadataName(this WellKnownType wellKnownType) {
        return MetadataNames[(int)wellKnownType - 1];
    }
}
