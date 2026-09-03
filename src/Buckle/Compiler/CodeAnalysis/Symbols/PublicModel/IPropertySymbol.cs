
namespace Buckle.CodeAnalysis.Symbols;

public interface IPropertySymbol : ISymbol {
    ITypeSymbol type { get; }
}
