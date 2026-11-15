
namespace Buckle.CodeAnalysis.Symbols;

public interface IAliasSymbol : ISymbol {
    ISymbol target { get; }
}
