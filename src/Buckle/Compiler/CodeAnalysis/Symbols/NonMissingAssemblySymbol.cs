using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NonMissingAssemblySymbol : AssemblySymbol {
    private readonly ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol> _emittedNameToTypeMap =
        new ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol>();

    private NamespaceSymbol _globalNamespace;

    internal sealed override bool isMissing => false;

    internal override NamespaceSymbol globalNamespace {
        get {
            if (_globalNamespace is null) {
                var allGlobalNamespaces = from m in modules select m.globalNamespace;
                var result = MergedNamespaceSymbol.Create(
                    new NamespaceExtent(this),
                    null,
                    allGlobalNamespaces.AsImmutable()
                );

                Interlocked.CompareExchange(ref _globalNamespace, result, null);
            }

            return _globalNamespace;
        }
    }

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

    internal sealed override NamedTypeSymbol? LookupDeclaredTopLevelMetadataType(ref MetadataTypeName emittedName) {
        NamedTypeSymbol result = null;

        result = LookupTopLevelMetadataTypeInCache(ref emittedName);

        if (result is not null) {
            if (!result.IsErrorType() && (object)result.containingAssembly == (object)this)
                return result;

            return null;
        } else {
            result = LookupDeclaredTopLevelMetadataTypeInModules(ref emittedName);

            if (result is null)
                return null;

            return CacheTopLevelMetadataType(ref emittedName, result);
        }
    }
}
