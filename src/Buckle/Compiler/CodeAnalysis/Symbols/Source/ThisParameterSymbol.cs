using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ThisParameterSymbol : ThisParameterSymbolBase {
    private readonly MethodSymbol _containingMethod;
    private readonly TypeSymbol _containingType;

    internal ThisParameterSymbol(MethodSymbol forMethod) : this(forMethod, forMethod.containingType) { }

    internal ThisParameterSymbol(MethodSymbol forMethod, TypeSymbol containingType) {
        _containingMethod = forMethod;
        _containingType = containingType;
    }

    internal override TypeWithAnnotations typeWithAnnotations => new TypeWithAnnotations(_containingType, false);

    public override RefKind refKind {
        get {
            if (containingType?.typeKind != TypeKind.Struct)
                return RefKind.None;

            // TODO need this enum member?
            // if (_containingMethod?.methodKind == MethodKind.Constructor)
            //     return RefKind.Out;

            return RefKind.Ref;
        }
    }

    internal override TextLocation location => _containingMethod?.location;

    internal override Symbol containingSymbol => (Symbol)_containingMethod ?? _containingType;

    internal override ScopedKind effectiveScope {
        get {
            var scope = _containingType.IsStructType() ? ScopedKind.ScopedRef : ScopedKind.None;

            if (scope != ScopedKind.None && hasUnscopedRefAttribute)
                return ScopedKind.None;

            return scope;
        }
    }

    internal override bool hasUnscopedRefAttribute => _containingMethod.HasUnscopedRefAttributeOnMethod();
}
