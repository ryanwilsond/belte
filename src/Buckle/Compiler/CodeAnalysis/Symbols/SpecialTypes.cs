using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypes {
    // TODO Eventually these will be inside a namespace
    private static readonly Dictionary<string, SpecialType> NameToTypeMap = new Dictionary<string, SpecialType>() {
        { "global::Object", SpecialType.Object },
        { "global::List`1", SpecialType.List },
        { "global::Dictionary`2", SpecialType.Dictionary },
        { "global::void", SpecialType.Void },
        { "global::int", SpecialType.Int },
        { "global::decimal", SpecialType.Decimal },
        { "global::int8", SpecialType.Int8 },
        { "global::int16", SpecialType.Int16 },
        { "global::int32", SpecialType.Int32 },
        { "global::int64", SpecialType.Int64 },
        { "global::uint8", SpecialType.UInt8 },
        { "global::uint16", SpecialType.UInt16 },
        { "global::uint32", SpecialType.UInt32 },
        { "global::uint64", SpecialType.UInt64 },
        { "global::float32", SpecialType.Float32 },
        { "global::float64", SpecialType.Float64 },
        { "global::intptr", SpecialType.IntPtr },
        { "global::uintptr", SpecialType.UIntPtr },
        { "global::bool", SpecialType.Bool },
        { "global::char", SpecialType.Char },
        { "global::string", SpecialType.String },
        { "global::type", SpecialType.Type },
        { "global::any", SpecialType.Any },
        { "global::Vec2", SpecialType.Vec2 },
        { "global::Sprite", SpecialType.Sprite },
        { "global::Text", SpecialType.Text },
        { "global::Rect", SpecialType.Rect },
        { "global::Texture", SpecialType.Texture },
        { "global::Sound", SpecialType.Sound },
        { "global::Exception", SpecialType.Exception },
    };

    internal static SpecialType GetTypeFromMetadataName(string metadataName) {
        if (NameToTypeMap.TryGetValue(metadataName, out var specialType))
            return specialType;

        return SpecialType.None;
    }
}
