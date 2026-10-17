
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class OverriddenMethodTemplateParameterMap : OverriddenMethodTemplateParameterMapBase {
    internal OverriddenMethodTemplateParameterMap(SourceOrdinaryMethodOrUserDefinedOperatorSymbol overridingMethod)
        : base(overridingMethod) { }

    private protected override MethodSymbol GetOverriddenMethod(SourceOrdinaryMethodOrUserDefinedOperatorSymbol overridingMethod) {
        MethodSymbol method = overridingMethod;

        do {
            method = method.overriddenMethod;
        } while (method is not null && method.isOverride);

        return method;
    }
}
