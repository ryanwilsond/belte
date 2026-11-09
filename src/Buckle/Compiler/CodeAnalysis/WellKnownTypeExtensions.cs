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
        "System.Int64",
        "System.Int32",
        "System.Int16",
        "System.SByte",
        "System.UInt64",
        "System.UInt32",
        "System.UInt16",
        "System.Byte",
        "System.Char",
        "System.Single",
        "System.Double",
        "System.Boolean",
        "System.Delegate",
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
        "Buckle.SimpleTextLocation",
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

    internal static WellKnownType FromSpecialType(SpecialType specialType) {
        switch (specialType) {
            case SpecialType.String: return WellKnownType.System_String;
            case SpecialType.Type: return WellKnownType.System_Type;
            case SpecialType.Int: return WellKnownType.System_Int64;
            case SpecialType.Int64: return WellKnownType.System_Int64;
            case SpecialType.Int32: return WellKnownType.System_Int32;
            case SpecialType.Int16: return WellKnownType.System_Int16;
            case SpecialType.Int8: return WellKnownType.System_SByte;
            case SpecialType.UInt64: return WellKnownType.System_UInt64;
            case SpecialType.UInt32: return WellKnownType.System_UInt32;
            case SpecialType.UInt16: return WellKnownType.System_UInt16;
            case SpecialType.UInt8: return WellKnownType.System_Byte;
            case SpecialType.Char: return WellKnownType.System_Char;
            case SpecialType.Float32: return WellKnownType.System_Single;
            case SpecialType.Decimal: return WellKnownType.System_Double;
            case SpecialType.Float64: return WellKnownType.System_Double;
            case SpecialType.Bool: return WellKnownType.System_Boolean;
            default:
                return WellKnownType.None;
        }
    }
}
