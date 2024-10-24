using Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SubstitutedErrorTypeSymbol : ErrorTypeSymbol {
    private readonly ErrorTypeSymbol _originalDefinition;
    private int _hashCode;

    private protected SubstitutedErrorTypeSymbol(ErrorTypeSymbol originalDefinition) {
        _originalDefinition = originalDefinition;
    }

    public override string name => _originalDefinition.name;

    internal override NamedTypeSymbol originalDefinition => _originalDefinition;

    internal override int arity => _originalDefinition.arity;

    internal override bool mangleName => _originalDefinition.mangleName;

    internal override DiagnosticInfo errorInfo => _originalDefinition.errorInfo;

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }
}
