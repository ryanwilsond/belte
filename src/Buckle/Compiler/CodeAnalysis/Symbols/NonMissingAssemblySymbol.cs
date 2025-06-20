using System.Collections.Concurrent;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NonMissingAssemblySymbol : AssemblySymbol {
    private readonly ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol> _emittedNameToTypeMap =
        new ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol>();

    internal sealed override bool isMissing => false;

    internal sealed override NamedTypeSymbol LookupDeclaredOrForwardedTopLevelMetadataType(
        ref MetadataTypeName emittedName,
        ConsList<AssemblySymbol>? visitedAssemblies) {
        var result = LookupTopLevelMetadataTypeInCache(ref emittedName);

        if (result is not null) {
            return result;
        } else {
            result = LookupDeclaredTopLevelMetadataTypeInModules(ref emittedName);
            result ??= TryLookupForwardedMetadataTypeWithCycleDetection(ref emittedName, visitedAssemblies);

            return CacheTopLevelMetadataType(
                ref emittedName,
                result ?? new MissingMetadataTypeSymbol.TopLevel(modules[0], ref emittedName)
            );
        }
    }

    private NamedTypeSymbol LookupTopLevelMetadataTypeInCache(ref MetadataTypeName emittedName) {
        if (_emittedNameToTypeMap.TryGetValue(emittedName.ToKey(), out var result))
            return result;

        return null;
    }

    private NamedTypeSymbol LookupDeclaredTopLevelMetadataTypeInModules(ref MetadataTypeName emittedName) {
        foreach (var module in modules) {
            var result = module.LookupTopLevelMetadataType(ref emittedName);

            if (result is not null)
                return result;
        }

        return null;
    }

    private NamedTypeSymbol CacheTopLevelMetadataType(
        ref MetadataTypeName emittedName,
        NamedTypeSymbol result) {
        NamedTypeSymbol result1;
        result1 = _emittedNameToTypeMap.GetOrAdd(emittedName.ToKey(), result);
        System.Diagnostics.Debug.Assert(TypeSymbol.Equals(result1, result, TypeCompareKind.ConsiderEverything));
        return result1;
    }
}
