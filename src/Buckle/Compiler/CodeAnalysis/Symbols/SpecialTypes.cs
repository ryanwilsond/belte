
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SpecialTypes {
    // TODO Eventually these will be inside a namespace
    private static readonly Dictionary<string, SpecialType> NameToTypeMap = new Dictionary<string, SpecialType>() {
        { "Object", SpecialType.Object },
        { "List<type>", SpecialType.List },
        { "Dictionary<type,type>", SpecialType.Dictionary },
    };

    internal static SpecialType GetTypeFromMetadataName(string metadataName) {
        if (NameToTypeMap.TryGetValue(metadataName, out var specialType))
            return specialType;

        return SpecialType.None;
    }
}
