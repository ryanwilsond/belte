
namespace Buckle.CodeAnalysis.Symbols;

public interface INamespaceSymbol : INamespaceOrTypeSymbol {
    bool isGlobalNamespace { get; }

    NamespaceKind namespaceKind { get; }

    Compilation containingCompilation { get; }
}
