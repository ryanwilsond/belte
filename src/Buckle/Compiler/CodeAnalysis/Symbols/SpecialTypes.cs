using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypes {
    // TODO Eventually these will be inside a namespace
    private static readonly Dictionary<string, SpecialType> NameToTypeMap = new Dictionary<string, SpecialType>() {
        { "global::Object", SpecialType.Object },
        { "global::List<type>", SpecialType.List },
        { "global::Dictionary<type,type>", SpecialType.Dictionary },
        { "global::void", SpecialType.Void },
        { "global::int", SpecialType.Int },
        { "global::decimal", SpecialType.Decimal },
        { "global::bool", SpecialType.Bool },
        { "global::char", SpecialType.Char },
        { "global::string", SpecialType.String },
        { "global::type", SpecialType.Type },
        { "global::any", SpecialType.Any },
        { "global::Vec2", SpecialType.Vec2 },
        { "global::Sprite", SpecialType.Sprite },
    };

    internal static SpecialType GetTypeFromMetadataName(string metadataName) {
        if (NameToTypeMap.TryGetValue(metadataName, out var specialType))
            return specialType;

        return SpecialType.None;
    }
}
