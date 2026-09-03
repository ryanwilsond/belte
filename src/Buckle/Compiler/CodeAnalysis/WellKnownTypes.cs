using System.Collections.Generic;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

internal static class WellKnownTypes {
    internal const int PECount = (int)WellKnownType.LastPEType - (int)WellKnownType.FirstPEType + 1;

    private static readonly Dictionary<string, WellKnownType> NameToTypeMap = new Dictionary<string, WellKnownType>() {
        { "global::List`1", WellKnownType.List },
        { "global::Dictionary`2", WellKnownType.Dictionary },
        { "global::Enumerator`1", WellKnownType.Enumerator },
        { "global::ValueTuple`1", WellKnownType.ValueTuple_T1 },
        { "global::ValueTuple`2", WellKnownType.ValueTuple_T2 },
        { "global::ValueTuple`3", WellKnownType.ValueTuple_T3 },
        { "global::ValueTuple`4", WellKnownType.ValueTuple_T4 },
        { "global::ValueTuple`5", WellKnownType.ValueTuple_T5 },
        { "global::ValueTuple`6", WellKnownType.ValueTuple_T6 },
        { "global::ValueTuple`7", WellKnownType.ValueTuple_T7 },
        { "global::ValueTuple`8", WellKnownType.ValueTuple_TRest },
        { "global::Array`1", WellKnownType.Array },
        { "global::DllImportAttribute", WellKnownType.DllImportAttribute },
        { "global::UnmanagedAttribute", WellKnownType.UnmanagedAttribute },
        { "global::MustUseReturnValueAttribute", WellKnownType.MustUseReturnValueAttribute },
    };

    internal static WellKnownType GetTypeFromMetadataName(string metadataName) {
        if (NameToTypeMap.TryGetValue(metadataName, out var wellKnownType))
            return wellKnownType;

        return WellKnownType.None;
    }

    internal static WellKnownType GetTypeFromMetadataName(NamedTypeSymbol type) {
        string emittedName = null;

        if (type.containingSymbol is not null)
            emittedName = type.containingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedNameFormat);

        var normalizedMetadataName = type.arity == 0 || type.mangleName
            ? type.metadataName
            : type.metadataName + "`" + type.arity;

        emittedName = MetadataHelpers.BuildQualifiedName(emittedName, normalizedMetadataName);

        return GetTypeFromMetadataName(emittedName);
    }
}
