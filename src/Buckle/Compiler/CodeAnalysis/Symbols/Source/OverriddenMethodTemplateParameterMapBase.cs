using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class OverriddenMethodTemplateParameterMapBase {
    private TemplateMap _lazyTemplateMap;
    private MethodSymbol _lazyOverriddenMethod = ErrorMethodSymbol.UnknownMethod;

    private protected OverriddenMethodTemplateParameterMapBase(SourceOrdinaryMethodSymbol overridingMethod) {
        this.overridingMethod = overridingMethod;
    }

    internal SourceOrdinaryMethodSymbol overridingMethod { get; }

    internal TemplateMap templateMap {
        get {
            if (_lazyTemplateMap is null) {
                var overriddenMethod = _overriddenMethod;

                if (overriddenMethod is not null) {
                    var overriddenTemplateParameters = overriddenMethod.templateParameters;
                    var overridingTemplateParameters = overridingMethod.templateParameters;

                    var map = new TemplateMap(overriddenTemplateParameters, overridingTemplateParameters);
                    Interlocked.CompareExchange(ref _lazyTemplateMap, map, null);
                }
            }

            return _lazyTemplateMap;
        }
    }

    private MethodSymbol _overriddenMethod {
        get {
            if (ReferenceEquals(_lazyOverriddenMethod, ErrorMethodSymbol.UnknownMethod)) {
                Interlocked.CompareExchange(
                    ref _lazyOverriddenMethod,
                    GetOverriddenMethod(overridingMethod),
                    ErrorMethodSymbol.UnknownMethod
                );
            }

            return _lazyOverriddenMethod;
        }
    }

    internal TemplateParameterSymbol GetOverriddenTemplateParameter(int ordinal) {
        var overridingMethod = this.overridingMethod;
        return overridingMethod?.templateParameters[ordinal];
    }

    private protected abstract MethodSymbol GetOverriddenMethod(SourceOrdinaryMethodSymbol overridingMethod);
}
