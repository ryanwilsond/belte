
namespace Buckle.CodeAnalysis;

internal static class WellKnownTypeExtensions {
    private static readonly string[] MetadataNames = [
        "Enumerator`1",
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
        "Array`1",
        "DllImportAttribute",
        "UnmanagedAttribute",
        "MustUseReturnValueAttribute",
        "System.Exception",
        "System.Collections.IEnumerable",
        "System.Collections.Generic.IEnumerable`1",
        "System.Collections.IEnumerator",
        "System.Collections.Generic.IEnumerator`1",
        "System.Attribute",
        "System.AttributeUsageAttribute",
        "System.String",
        "Belte.NoAllocAttribute",
        "Belte.NoThrowAttribute",
        "Belte.PureAttribute",
        "Belte.CompilerServices.BelteMetadataAttribute",
        "Belte.NullabilityAttribute",
        "Belte.ConstMethodAttribute",
        "Belte.ConstParamAttribute",
        "Belte.Graphics.Vec2",
        "Belte.Graphics.Sprite",
        "Belte.Graphics.Text",
        "Belte.Graphics.Rect",
        "Belte.Graphics.Texture",
        "Belte.Graphics.Sound",
    ];

    internal static bool ShouldEmit(this WellKnownType wellKnownType, bool noStdLib, bool includeGraphicsTypes) {
        switch (wellKnownType) {
            case WellKnownType.None:
            case WellKnownType.List:
            case WellKnownType.Dictionary:
            case WellKnownType.Enumerator:
            case WellKnownType.Array:
                return true;
            case WellKnownType.Belte_Graphics_Vec2 when includeGraphicsTypes && noStdLib:
            case WellKnownType.Belte_Graphics_Sprite when includeGraphicsTypes && noStdLib:
            case WellKnownType.Belte_Graphics_Text when includeGraphicsTypes && noStdLib:
            case WellKnownType.Belte_Graphics_Rect when includeGraphicsTypes && noStdLib:
            case WellKnownType.Belte_Graphics_Texture when includeGraphicsTypes && noStdLib:
            case WellKnownType.Belte_Graphics_Sound when includeGraphicsTypes && noStdLib:
                return true;
            case WellKnownType.ValueTuple_T1 when noStdLib:
            case WellKnownType.ValueTuple_T2 when noStdLib:
            case WellKnownType.ValueTuple_T3 when noStdLib:
            case WellKnownType.ValueTuple_T4 when noStdLib:
            case WellKnownType.ValueTuple_T5 when noStdLib:
            case WellKnownType.ValueTuple_T6 when noStdLib:
            case WellKnownType.ValueTuple_T7 when noStdLib:
            case WellKnownType.ValueTuple_TRest when noStdLib:
            case WellKnownType.UnmanagedAttribute when noStdLib:
            case WellKnownType.DllImportAttribute when noStdLib:
            case WellKnownType.MustUseReturnValueAttribute when noStdLib:
                return true;
            default:
                return false;
        }
    }

    internal static string GetMetadataName(this WellKnownType wellKnownType) {
        return MetadataNames[(int)(wellKnownType - WellKnownType.First)];
    }
}
