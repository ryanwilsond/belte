
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class WithClassTemplateParametersBinder : WithTemplateParametersBinder {
    private readonly NamedTypeSymbol _namedType;
    private Dictionary<string, List<TemplateParameterSymbol>> _lazyTemplateParameterMap;

    internal WithClassTemplateParametersBinder(NamedTypeSymbol container, Binder next)
        : base(next) {
        _namedType = container;
    }

    private protected override Dictionary<string, List<TemplateParameterSymbol>> _templateParameterMap {
        get {
            if (_lazyTemplateParameterMap is null) {
                var result = new Dictionary<string, List<TemplateParameterSymbol>>();

                foreach (var templateParameter in _namedType.templateParameters) {
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

    internal override bool IsAccessibleHelper(
        Symbol symbol,
        TypeSymbol accessThroughType,
        out bool failedThroughTypeCheck) {
        return IsSymbolAccessibleConditional(symbol, _namedType, accessThroughType, out failedThroughTypeCheck);
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        if (CanConsiderTypeParameters(options)) {
            foreach (var parameter in _namedType.templateParameters) {
                if (originalBinder.CanAddLookupSymbolInfo(parameter, options, result, null))
                    result.AddSymbol(parameter, parameter.name, 0);
            }
        }
    }
}
