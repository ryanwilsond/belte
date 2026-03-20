using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NonMissingModuleSymbol : ModuleSymbol {
    private ModuleReferences<AssemblySymbol> _moduleReferences;

    internal sealed override bool isMissing => false;

    internal sealed override ImmutableArray<AssemblyIdentity> referencedAssemblies => _moduleReferences.identities;

    internal sealed override ImmutableArray<AssemblySymbol> referencedAssemblySymbols => _moduleReferences.symbols;

    internal ImmutableArray<UnifiedAssembly<AssemblySymbol>> GetUnifiedAssemblies() {
        return _moduleReferences.unifiedAssemblies;
    }

    internal override bool hasUnifiedReferences => GetUnifiedAssemblies().Length > 0;

    internal override void SetReferences(
        ModuleReferences<AssemblySymbol> moduleReferences,
        SourceAssemblySymbol originatingSourceAssemblyDebugOnly = null) {
        _moduleReferences = moduleReferences;
    }

    internal sealed override NamedTypeSymbol LookupTopLevelMetadataType(ref MetadataTypeName emittedName) {
        NamedTypeSymbol result;
        var scope = globalNamespace.LookupNestedNamespace(emittedName.namespaceSegmentsMemory);

        if (scope is null)
            result = null;
        else
            result = scope.LookupMetadataType(ref emittedName);

        return result;
    }
}
