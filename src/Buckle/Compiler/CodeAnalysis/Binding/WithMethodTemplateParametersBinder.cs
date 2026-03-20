using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class WithMethodTemplateParametersBinder : WithTemplateParametersBinder {
    private readonly MethodSymbol _methodSymbol;
    private Dictionary<string, List<TemplateParameterSymbol>> _lazyTemplateParameterMap;

    internal WithMethodTemplateParametersBinder(MethodSymbol methodSymbol, Binder next) : base(next) {
        _methodSymbol = methodSymbol;
    }

    private protected override bool _inExecutableBinder => false;

    internal override Symbol containingMember => _methodSymbol;

    private protected override Dictionary<string, List<TemplateParameterSymbol>> _templateParameterMap {
        get {
            if (_lazyTemplateParameterMap is null) {
                var result = new Dictionary<string, List<TemplateParameterSymbol>>();

                foreach (var templateParameter in _methodSymbol.templateParameters) {
                    if (!result.ContainsKey(templateParameter.name)) {
                        result.Add(templateParameter.name, [templateParameter]);
                    } else {
                        result.TryGetValue(templateParameter.name, out var list);
                        list.Add(templateParameter);
                    }
                }

                Interlocked.CompareExchange(ref _lazyTemplateParameterMap, result, null);
            }

            return _lazyTemplateParameterMap;
        }
    }

    private protected override LookupOptions _lookupMask => LookupOptions.MustNotBeMethodTemplateParameter;

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        if (CanConsiderTypeParameters(options)) {
            foreach (var parameter in _methodSymbol.templateParameters) {
                if (originalBinder.CanAddLookupSymbolInfo(parameter, options, result, null))
                    result.AddSymbol(parameter, parameter.name, 0);
            }
        }
    }
}
