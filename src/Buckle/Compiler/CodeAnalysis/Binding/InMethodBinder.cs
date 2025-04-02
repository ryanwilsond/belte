using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class InMethodBinder : LocalScopeBinder {
    private Dictionary<string, List<ParameterSymbol>> _lazyParameterMap;
    private readonly MethodSymbol _methodSymbol;
    private Dictionary<string, Symbol> _lazyDefinitionMap;

    internal InMethodBinder(MethodSymbol owner, Binder enclosing)
        : base(enclosing, enclosing.flags & ~BinderFlags.AllClearedAtExecutableCodeBoundary) {
        _methodSymbol = owner;
    }

    internal override Symbol containingMember => _methodSymbol;

    internal override bool isInMethodBody => true;

    internal override bool isNestedFunctionBinder => _methodSymbol.methodKind == MethodKind.LocalFunction;

    internal override SynthesizedLabelSymbol breakLabel => null;

    internal override SynthesizedLabelSymbol continueLabel => null;

    private protected override bool _inExecutableBinder => true;

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        var parameterMap = _lazyParameterMap;

        if (parameterMap is null) {
            var parameters = _methodSymbol.parameters;
            parameterMap = new Dictionary<string, List<ParameterSymbol>>(parameters.Length);

            foreach (var parameter in parameters) {
                if (!parameterMap.ContainsKey(parameter.name)) {
                    parameterMap.Add(parameter.name, [parameter]);
                } else {
                    parameterMap.TryGetValue(parameter.name, out var list);
                    list.Add(parameter);
                }
            }

            _lazyParameterMap = parameterMap;
        }

        if (parameterMap.TryGetValue(name, out var parameterSymbols)) {
            foreach (var parameterSymbol in parameterSymbols)
                result.MergeEqual(originalBinder.CheckViability(parameterSymbol, arity, options, null, diagnose));
        }
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        if (options.CanConsiderMembers()) {
            foreach (var parameter in _methodSymbol.parameters) {
                if (originalBinder.CanAddLookupSymbolInfo(parameter, options, result, null))
                    result.AddSymbol(parameter, parameter.name, 0);
            }
        }
    }

    private static void RecordDefinition<T>(Dictionary<string, Symbol> declarationMap, ImmutableArray<T> definitions)
        where T : Symbol {
        foreach (Symbol s in definitions)
            declarationMap.TryAdd(s.name, s);
    }

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken nameToken) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken nameToken) {
        return null;
    }

    private static bool ReportConflictWithParameter(
        Symbol parameter,
        Symbol newSymbol,
        string name,
        TextLocation newLocation,
        BelteDiagnosticQueue diagnostics) {
        var parameterKind = parameter.kind;
        var newSymbolKind = newSymbol is null ? SymbolKind.Parameter : newSymbol.kind;

        if (newSymbolKind == SymbolKind.ErrorType)
            return true;

        if (parameterKind == SymbolKind.Parameter) {
            switch (newSymbolKind) {
                case SymbolKind.Parameter:
                case SymbolKind.Local:
                    diagnostics.Push(Error.LocalShadowsParameter(newLocation, name));
                    return true;
                case SymbolKind.Method:
                    if (((MethodSymbol)newSymbol).methodKind == MethodKind.LocalFunction)
                        goto case SymbolKind.Parameter;

                    break;
                case SymbolKind.TemplateParameter:
                    return false;
            }
        }

        if (parameterKind == SymbolKind.TemplateParameter) {
            switch (newSymbolKind) {
                case SymbolKind.Parameter:
                case SymbolKind.Local:
                    diagnostics.Push(Error.ParameterOrLocalShadowsTemplateParameter(newLocation, name));
                    return true;
                case SymbolKind.Method:
                    if (((MethodSymbol)newSymbol).methodKind == MethodKind.LocalFunction)
                        goto case SymbolKind.Parameter;

                    break;
                case SymbolKind.TemplateParameter:
                    return false;
            }
        }

        diagnostics.Push(Error.InternalError(newLocation));
        return true;
    }

    internal override bool EnsureSingleDefinition(
        Symbol symbol,
        string name,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        var parameters = _methodSymbol.parameters;
        var templateParameters = _methodSymbol.templateParameters;

        if (parameters.IsEmpty && templateParameters.IsEmpty)
            return false;

        var map = _lazyDefinitionMap;

        if (map is null) {
            map = [];
            RecordDefinition(map, parameters);
            RecordDefinition(map, templateParameters);

            _lazyDefinitionMap = map;
        }

        if (map.TryGetValue(name, out var existingDeclaration))
            return ReportConflictWithParameter(existingDeclaration, symbol, name, location, diagnostics);

        return false;
    }
}
