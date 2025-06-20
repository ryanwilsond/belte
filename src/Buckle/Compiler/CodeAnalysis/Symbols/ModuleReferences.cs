using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ModuleReferences<TAssemblySymbol> where TAssemblySymbol : AssemblySymbol {
    internal readonly ImmutableArray<AssemblyIdentity> identities;

    internal readonly ImmutableArray<TAssemblySymbol> symbols;

    internal readonly ImmutableArray<UnifiedAssembly<TAssemblySymbol>> unifiedAssemblies;

    internal ModuleReferences(
        ImmutableArray<AssemblyIdentity> identities,
        ImmutableArray<TAssemblySymbol> symbols,
        ImmutableArray<UnifiedAssembly<TAssemblySymbol>> unifiedAssemblies) {
        this.identities = identities;
        this.symbols = symbols;
        this.unifiedAssemblies = unifiedAssemblies;
    }
}
