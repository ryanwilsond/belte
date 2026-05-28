using System.Collections.Generic;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

internal static class WellKnownTypes {
    private static readonly Dictionary<string, WellKnownType> NameToTypeMap = new Dictionary<string, WellKnownType>() {
        { "global::List`1", WellKnownType.List },
        { "global::Dictionary`2", WellKnownType.Dictionary },
        { "global::Enumerator`1", WellKnownType.Enumerator },
        { "global::Exception", WellKnownType.Exception },
        { "global::Vec2", WellKnownType.Vec2 },
        { "global::Sprite", WellKnownType.Sprite },
        { "global::Text", WellKnownType.Text },
        { "global::Rect", WellKnownType.Rect },
        { "global::Texture", WellKnownType.Texture },
        { "global::Sound", WellKnownType.Sound },
    };

    internal static WellKnownType GetTypeFromMetadataName(string metadataName) {
        if (NameToTypeMap.TryGetValue(metadataName, out var specialType))
            return specialType;

        return WellKnownType.None;
    }

    internal static WellKnownType GetTypeFromMetadataName(NamedTypeSymbol type) {
        string emittedName = null;

        if (type.containingSymbol is not null)
            emittedName = type.containingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedNameFormat);

        emittedName = MetadataHelpers.BuildQualifiedName(emittedName, type.metadataName);

        return GetTypeFromMetadataName(emittedName);
    }
}
