
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedNamedTypeSymbol : NamedTypeSymbol {
    protected WrappedNamedTypeSymbol(NamedTypeSymbol underlyingType) {
        this.underlyingType = underlyingType;
    }

    internal NamedTypeSymbol underlyingType { get; }

    internal override int arity => underlyingType.arity;
}
