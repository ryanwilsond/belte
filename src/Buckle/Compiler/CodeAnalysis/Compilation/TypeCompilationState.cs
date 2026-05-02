using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class TypeCompilationState {
    internal readonly ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts;

    internal ArrayBuilder<(MethodSymbol, BoundBlockStatement)> synthesizedMethods;
    internal ArrayBuilder<(NamedTypeSymbol, NamedTypeSymbol)> synthesizedTypes;
    internal ArrayBuilder<(MethodSymbol, EvaluatorSlotManager)> methodLayouts;

    private Dictionary<MethodSymbol, MethodSymbol> _constructorInitializers;
    private Dictionary<MethodSymbol, MethodSymbol> _wrappers;

    internal ImportChain currentImportChain;

    internal TypeCompilationState(
        NamedTypeSymbol type,
        Compilation compilation,
        ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts) {
        this.type = type;
        this.compilation = compilation;
        this.typeLayouts = typeLayouts;
    }

    internal Compilation compilation { get; }

    internal NamedTypeSymbol type { get; }

    internal int nextWrapperMethodIndex => _wrappers is null ? 0 : _wrappers.Count;

    internal void Free() {
        synthesizedMethods?.Free();
        synthesizedMethods = null;
        synthesizedTypes?.Free();
        synthesizedTypes = null;
        methodLayouts?.Free();
        methodLayouts = null;

        _constructorInitializers = null;
    }

    internal void AddMethodWrapper(MethodSymbol method, MethodSymbol wrapper, BoundBlockStatement body) {
        AddSynthesizedMethod(wrapper, body);
        _wrappers ??= [];
        _wrappers.Add(method, wrapper);
    }

    internal MethodSymbol GetMethodWrapper(MethodSymbol method) {
        return _wrappers is not null && _wrappers.TryGetValue(method, out var wrapper) ? wrapper : null;
    }

    internal void AddMethodLayout(MethodSymbol symbol, EvaluatorSlotManager layout) {
        methodLayouts ??= ArrayBuilder<(MethodSymbol, EvaluatorSlotManager)>.GetInstance();
        methodLayouts.Add((symbol, layout));
    }

    internal void AddSynthesizedType(NamedTypeSymbol containingType, NamedTypeSymbol type) {
        synthesizedTypes ??= ArrayBuilder<(NamedTypeSymbol, NamedTypeSymbol)>.GetInstance();
        synthesizedTypes.Add((containingType, type));
    }

    internal void AddSynthesizedMethod(MethodSymbol symbol, BoundBlockStatement body) {
        synthesizedMethods ??= ArrayBuilder<(MethodSymbol, BoundBlockStatement)>.GetInstance();
        synthesizedMethods.Add((symbol, body));
    }

    internal void ReportConstructorInitializerCycles(
        MethodSymbol method1,
        MethodSymbol method2,
        SyntaxNode syntaxNode,
        BelteDiagnosticQueue diagnostics) {
        if (method1 == method2)
            throw ExceptionUtilities.Unreachable();

        if (_constructorInitializers is null) {
            _constructorInitializers = new Dictionary<MethodSymbol, MethodSymbol> {
                { method1, method2 }
            };

            return;
        }

        var next = method2;

        while (true) {
            if (_constructorInitializers.TryGetValue(next, out next)) {
                if (method1 == next) {
                    // TODO Initializer recursive cycle error
                    return;
                }
            } else {
                _constructorInitializers.Add(method1, method2);
                return;
            }
        }
    }
}
