using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal class PEModuleBuilder {
    internal static HashSet<NamedTypeSymbol> GetForwardedTypes(SourceAssemblySymbol sourceAssembly) {
        var seenTopLevelForwardedTypes = new HashSet<NamedTypeSymbol>();
        GetForwardedTypes(seenTopLevelForwardedTypes, sourceAssembly.GetSourceDecodedWellKnownAttributeData());
        GetForwardedTypes(seenTopLevelForwardedTypes, sourceAssembly.GetNetModuleDecodedWellKnownAttributeData());
        return seenTopLevelForwardedTypes;
    }

    private static void GetForwardedTypes(
        HashSet<NamedTypeSymbol> seenTopLevelTypes,
        CommonAssemblyWellKnownAttributeData<NamedTypeSymbol> wellKnownAttributeData) {
        if (wellKnownAttributeData?.forwardedTypes?.Count > 0) {
            var stack = ArrayBuilder<(NamedTypeSymbol type, int parentIndex)>.GetInstance();
            IEnumerable<NamedTypeSymbol> orderedForwardedTypes = wellKnownAttributeData.forwardedTypes;

            foreach (var forwardedType in orderedForwardedTypes) {
                var originalDefinition = forwardedType.originalDefinition;

                if (!seenTopLevelTypes.Add(originalDefinition))
                    continue;
            }

            stack.Free();
        }
    }
}
