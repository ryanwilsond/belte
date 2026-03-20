
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class OverriddenMethodTemplateParameterMap : OverriddenMethodTemplateParameterMapBase {
    internal OverriddenMethodTemplateParameterMap(SourceOrdinaryMethodSymbol overridingMethod)
        : base(overridingMethod) { }

    private protected override MethodSymbol GetOverriddenMethod(SourceOrdinaryMethodSymbol overridingMethod) {
        MethodSymbol method = overridingMethod;

        do {
            method = method.overriddenMethod;
        } while (method is not null && method.isOverride);

        return method;
    }
}
