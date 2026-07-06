using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

public interface INamespaceOrTypeSymbol : ISymbol {
    INamespaceSymbol containingNamespace { get; }

    ImmutableArray<ISymbol> GetMembers();

    ImmutableArray<ISymbol> GetMembers(string name);

    ImmutableArray<INamedTypeSymbol> GetTypeMembers();

    ImmutableArray<INamedTypeSymbol> GetTypeMembers(string name);
}
