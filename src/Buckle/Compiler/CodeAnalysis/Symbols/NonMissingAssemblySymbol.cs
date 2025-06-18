using System.Collections.Concurrent;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NonMissingAssemblySymbol : AssemblySymbol {
    private readonly ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol> _emittedNameToTypeMap =
        new ConcurrentDictionary<MetadataTypeName.Key, NamedTypeSymbol>();

    internal sealed override bool isMissing => false;
}
