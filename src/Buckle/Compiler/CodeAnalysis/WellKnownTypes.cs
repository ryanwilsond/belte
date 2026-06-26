using System.Collections.Generic;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

internal static class WellKnownTypes {
    internal const int PECount = (int)WellKnownType.LastPEType - (int)WellKnownType.FirstPEType + 1;

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
        { "global::ValueTuple`1", WellKnownType.ValueTuple_T1 },
        { "global::ValueTuple`2", WellKnownType.ValueTuple_T2 },
        { "global::ValueTuple`3", WellKnownType.ValueTuple_T3 },
        { "global::ValueTuple`4", WellKnownType.ValueTuple_T4 },
        { "global::ValueTuple`5", WellKnownType.ValueTuple_T5 },
        { "global::ValueTuple`6", WellKnownType.ValueTuple_T6 },
        { "global::ValueTuple`7", WellKnownType.ValueTuple_T7 },
        { "global::ValueTuple`8", WellKnownType.ValueTuple_TRest },
        { "global::Array`1", WellKnownType.Array },
        { "global::Attribute", WellKnownType.Attribute },
        { "global::DllImportAttribute", WellKnownType.DllImportAttribute },
        { "global::UnmanagedAttribute", WellKnownType.UnmanagedAttribute },
        { "global::MustUseReturnValueAttribute", WellKnownType.MustUseReturnValueAttribute },
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
