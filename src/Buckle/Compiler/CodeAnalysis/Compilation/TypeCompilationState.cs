using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class TypeCompilationState {
    private Dictionary<MethodSymbol, MethodSymbol> _constructorInitializers;
    private Dictionary<MethodSymbol, MethodSymbol> _wrappers;

    internal ArrayBuilder<(MethodSymbol, BoundBlockStatement)> synthesizedMethods;
    internal ArrayBuilder<(NamedTypeSymbol, NamedTypeSymbol)> synthesizedTypes;

    internal TypeCompilationState(NamedTypeSymbol type, Compilation compilation) {
        this.type = type;
        this.compilation = compilation;
    }

    internal Compilation compilation { get; }

    internal NamedTypeSymbol type { get; }

    internal int nextWrapperMethodIndex => _wrappers is null ? 0 : _wrappers.Count;

    internal void Free() {
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
