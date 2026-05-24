
namespace Buckle.CodeAnalysis.Symbols;

public interface IAliasSymbol : ISymbol {
    INamespaceOrTypeSymbol target { get; }
}
