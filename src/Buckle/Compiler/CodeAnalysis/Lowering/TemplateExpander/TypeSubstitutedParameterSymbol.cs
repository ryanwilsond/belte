using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class TypeSubstitutedParameterSymbol : WrappedParameterSymbol {
    private readonly ParameterSymbol _originalParameter;
    private readonly TypeWithAnnotations _type;

    internal TypeSubstitutedParameterSymbol(
        ParameterSymbol originalParameter,
        TypeWithAnnotations type) : base(originalParameter) {
        _originalParameter = originalParameter;
        _type = type;
    }

    // TODO This technically points to an obsolete method so this might need to change
    internal override Symbol containingSymbol => _originalParameter.containingSymbol;

    internal override TypeWithAnnotations typeWithAnnotations => _type;
}
