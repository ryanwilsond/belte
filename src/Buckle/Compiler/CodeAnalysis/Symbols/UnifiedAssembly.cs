
namespace Buckle.CodeAnalysis.Symbols;

internal readonly struct UnifiedAssembly<TAssemblySymbol> where TAssemblySymbol : AssemblySymbol {
    internal readonly AssemblyIdentity originalReference;

    internal readonly TAssemblySymbol targetAssembly;

    internal UnifiedAssembly(TAssemblySymbol targetAssembly, AssemblyIdentity originalReference) {
        this.originalReference = originalReference;
        this.targetAssembly = targetAssembly;
    }
}
