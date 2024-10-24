using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SubstitutedParameterSymbol : WrappedParameterSymbol {
    private object _mapOrType;

    internal SubstitutedParameterSymbol(MethodSymbol containingSymbol, TemplateMap map, ParameterSymbol originalParameter)
        : this((Symbol)containingSymbol, map, originalParameter) { }

    private SubstitutedParameterSymbol(Symbol containingSymbol, TemplateMap map, ParameterSymbol originalParameter)
        : base(originalParameter) {
        this.containingSymbol = containingSymbol;
        _mapOrType = map;
    }

    internal override ParameterSymbol originalDefinition => underlyingParameter.originalDefinition;

    internal override Symbol containingSymbol { get; }

    internal override TypeWithAnnotations typeWithAnnotations {
        get {
            var mapOrType = _mapOrType;

            if (mapOrType is TypeWithAnnotations type)
                return type;

            var substituted = ((TemplateMap)mapOrType)
                .SubstituteType(underlyingParameter.typeWithAnnotations)
                .type;

            // TODO Interlock this?
            _mapOrType = substituted;

            return substituted;
        }
    }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)this == obj)
            return true;

        var other = obj as SubstitutedParameterSymbol;

        return other is not null &&
            ordinal == other.ordinal &&
            containingSymbol.Equals(other.containingSymbol, compareKind);
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(containingSymbol, underlyingParameter.ordinal);
    }
}
