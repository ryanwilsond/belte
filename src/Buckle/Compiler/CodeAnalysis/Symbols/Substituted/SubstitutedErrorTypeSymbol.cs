
namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SubstitutedErrorTypeSymbol : ErrorTypeSymbol {
    private int _hashCode;

    private protected SubstitutedErrorTypeSymbol(ErrorTypeSymbol originalDefinition) {
        this.originalDefinition = originalDefinition;
    }

    public override string name => (originalDefinition as ErrorTypeSymbol).name;

    internal override NamedTypeSymbol originalDefinition { get; }

    internal override int arity => originalDefinition.arity;

    internal override bool mangleName => originalDefinition.mangleName;

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }
}
