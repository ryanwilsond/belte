using System.Diagnostics;

namespace Buckle.CodeAnalysis;

internal static class WellKnownTypeExtensions {
    internal static int Count = (int)WellKnownType.NextAvailable - (int)WellKnownType.First;

    private static readonly string[] MetadataNames = [
        "Enumerator`1",
        "ValueTuple`1",
        "ValueTuple`2",
        "ValueTuple`3",
        "ValueTuple`4",
        "ValueTuple`5",
        "ValueTuple`6",
        "ValueTuple`7",
        "ValueTuple`8",
        "System.Exception",
        "System.Collections.IEnumerable",
        "System.Collections.Generic.IEnumerable`1",
        "System.Collections.IEnumerator",
        "System.Collections.Generic.IEnumerator`1",
        "System.Attribute",
        "System.AttributeUsageAttribute",
        "System.String",
        "System.Activator",
        "System.InvalidOperationException",
        "System.Type",
        "Belte.NoAllocAttribute",
        "Belte.NoThrowAttribute",
        "Belte.PureAttribute",
        "Belte.CompilerServices.BelteMetadataAttribute",
        "Belte.NullabilityAttribute",
        "Belte.ConstMethodAttribute",
        "Belte.ConstParamAttribute",
        "Belte.ConstExprParamAttribute",
        "List`1",
        "Dictionary`2",
        "Result`2",
        "WrappedErrorException",
        "Belte.Graphics.Vec2",
        "Belte.Graphics.Sprite",
        "Belte.Graphics.Text",
        "Belte.Graphics.Rect",
        "Belte.Graphics.Texture",
        "Belte.Graphics.Sound",
        "Buckle.CodeAnalysis.Text.TextLocation",
    ];

#if DEBUG
    static WellKnownTypeExtensions() {
        Debug.Assert(MetadataNames.Length == Count);
    }
#endif

    internal static bool IsWellKnownType(this WellKnownType typeId) {
        Debug.Assert(typeId != WellKnownType.ExtSentinel);
        return typeId >= WellKnownType.First && typeId < WellKnownType.NextAvailable;
    }

    internal static string GetMetadataName(this WellKnownType wellKnownType) {
        return MetadataNames[(int)(wellKnownType - WellKnownType.First)];
    }
}
