using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal class LocalScopeBinder : Binder {
    private protected const int DefaultLocalSymbolArrayCapacity = 16;

    private ImmutableArray<LocalSymbol> _locals;
    private ImmutableArray<LocalFunctionSymbol> _localFunctions;
    private ImmutableArray<LabelSymbol> _labels;
    private Dictionary<string, LocalSymbol> _lazyLocalsMap;
    private Dictionary<string, LocalFunctionSymbol> _lazyLocalFunctionsMap;
    private Dictionary<string, LabelSymbol> _lazyLabelsMap;

    internal LocalScopeBinder(Binder next) : this(next, next.flags) { }

    internal LocalScopeBinder(Binder next, BinderFlags flags) : base(next, flags) { }

    internal sealed override ImmutableArray<LocalSymbol> locals {
        get {
            if (_locals.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(ref _locals, BuildLocals(), default);

            return _locals;
        }
    }

    private protected virtual ImmutableArray<LocalSymbol> BuildLocals() {
        return [];
    }

    internal sealed override ImmutableArray<LocalFunctionSymbol> localFunctions {
        get {
            if (_localFunctions.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(ref _localFunctions, BuildLocalFunctions(), default);

            return _localFunctions;
        }
    }

    private protected virtual ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        return [];
    }

    internal sealed override ImmutableArray<LabelSymbol> labels {
        get {
            if (_labels.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(ref _labels, BuildLabels(), default);

            return _labels;
        }
    }

    private protected virtual ImmutableArray<LabelSymbol> BuildLabels() {
        return [];
    }

    private Dictionary<string, LocalSymbol> _localsMap {
        get {
            if (_lazyLocalsMap == null && locals.Length > 0)
                _lazyLocalsMap = BuildMap(locals);

            return _lazyLocalsMap;
        }
    }

    private Dictionary<string, LocalFunctionSymbol> _localFunctionsMap {
        get {
            if (_lazyLocalFunctionsMap == null && localFunctions.Length > 0)
                _lazyLocalFunctionsMap = BuildMap(localFunctions);

            return _lazyLocalFunctionsMap;
        }
    }

    private Dictionary<string, LabelSymbol> _labelsMap {
        get {
            if (_lazyLabelsMap == null && labels.Length > 0)
                _lazyLabelsMap = BuildMap(labels);

            return _lazyLabelsMap;
        }
    }

    private static Dictionary<string, TSymbol> BuildMap<TSymbol>(ImmutableArray<TSymbol> array) where TSymbol : Symbol {
        var map = new Dictionary<string, TSymbol>();

        for (var i = array.Length - 1; i >= 0; i--) {
            var symbol = array[i];
            map[symbol.name] = symbol;
        }

        return map;
    }

    protected ImmutableArray<LocalSymbol> BuildLocals(SyntaxList<StatementSyntax> statements, Binder enclosingBinder) {
        var locals = ArrayBuilder<LocalSymbol>.GetInstance(DefaultLocalSymbolArrayCapacity);
        foreach (var statement in statements) {
            BuildLocals(enclosingBinder, statement, locals);
        }

        return locals.ToImmutableAndFree();
    }

    internal void BuildLocals(Binder enclosingBinder, StatementSyntax statement, ArrayBuilder<LocalSymbol> locals) {
        // TODO
    }

    private protected ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions(SyntaxList<StatementSyntax> statements) {
        ArrayBuilder<LocalFunctionSymbol> locals = null;
        foreach (var statement in statements)
            BuildLocalFunctions(statement, ref locals);

        return locals?.ToImmutableAndFree() ?? ImmutableArray<LocalFunctionSymbol>.Empty;
    }

    internal void BuildLocalFunctions(StatementSyntax statement, ref ArrayBuilder<LocalFunctionSymbol> locals) {
        var innerStatement = statement;

        if (innerStatement.kind == SyntaxKind.LocalFunctionStatement) {
            var declaration = (LocalFunctionStatementSyntax)innerStatement;

            locals ??= ArrayBuilder<LocalFunctionSymbol>.GetInstance();

            var localSymbol = MakeLocalFunction(declaration);
            locals.Add(localSymbol);
        }
    }

    private protected SourceLocalSymbol MakeLocal(
        VariableDeclarationSyntax declaration,
        LocalDeclarationKind kind,
        bool allowScoped,
        Binder initializerBinder = null) {
        return SourceLocalSymbol.MakeLocal(
            containingMember,
            this,
            allowRefKind: true,
            allowScoped: allowScoped,
            declaration.type,
            declaration.identifier,
            kind,
            declaration.initializer,
            initializerBinder
        );
    }

    private protected LocalFunctionSymbol MakeLocalFunction(LocalFunctionStatementSyntax declaration) {
        return new LocalFunctionSymbol(
            this,
            containingMember,
            declaration
        );
    }

    private protected void BuildLabels(SyntaxList<StatementSyntax> statements, ref ArrayBuilder<LabelSymbol> labels) {
        var containingMethod = (MethodSymbol)containingMember;

        foreach (var statement in statements)
            BuildLabels(containingMethod, statement, ref labels);
    }

    internal static void BuildLabels(
        MethodSymbol containingMethod,
        StatementSyntax statement,
        ref ArrayBuilder<LabelSymbol> labels) { }

    private protected override SourceLocalSymbol LookupLocal(SyntaxToken identifier) {
        if (_localsMap != null && _localsMap.TryGetValue(identifier.text, out var result)) {
            if (result.identifier == identifier)
                return (SourceLocalSymbol)result;

            foreach (var local in locals) {
                if (local.identifier == identifier)
                    return (SourceLocalSymbol)local;
            }
        }

        return base.LookupLocal(identifier);
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        if (_localFunctionsMap != null && _localFunctionsMap.TryGetValue(identifier.text, out var result)) {
            if (result.identifier == identifier)
                return result;

            foreach (var local in _localFunctions) {
                if (local.identifier == identifier)
                    return local;
            }
        }

        return base.LookupLocalFunction(identifier);
    }
}
