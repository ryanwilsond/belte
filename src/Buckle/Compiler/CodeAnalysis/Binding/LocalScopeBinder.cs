using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal class LocalScopeBinder : Binder {
    private protected const int DefaultLocalSymbolArrayCapacity = 16;

    private ImmutableArray<DataContainerSymbol> _locals;
    private ImmutableArray<LocalFunctionSymbol> _localFunctions;
    private ImmutableArray<LabelSymbol> _labels;
    private Dictionary<string, DataContainerSymbol> _lazyLocalsMap;
    private Dictionary<string, LocalFunctionSymbol> _lazyLocalFunctionsMap;
    private Dictionary<string, LabelSymbol> _lazyLabelsMap;

    internal LocalScopeBinder(Binder next) : this(next, next.flags) { }

    internal LocalScopeBinder(Binder next, BinderFlags flags) : base(next, flags) { }

    internal sealed override ImmutableArray<DataContainerSymbol> locals {
        get {
            if (_locals.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(ref _locals, BuildLocals(), default);

            return _locals;
        }
    }

    private protected virtual ImmutableArray<DataContainerSymbol> BuildLocals() {
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

    private Dictionary<string, DataContainerSymbol> _localsMap {
        get {
            if (_lazyLocalsMap is null && locals.Length > 0)
                _lazyLocalsMap = BuildMap(locals);

            return _lazyLocalsMap;
        }
    }

    private Dictionary<string, LocalFunctionSymbol> _localFunctionsMap {
        get {
            if (_lazyLocalFunctionsMap is null && localFunctions.Length > 0)
                _lazyLocalFunctionsMap = BuildMap(localFunctions);

            return _lazyLocalFunctionsMap;
        }
    }

    private Dictionary<string, LabelSymbol> _labelsMap {
        get {
            if (_lazyLabelsMap is null && labels.Length > 0)
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

    protected ImmutableArray<DataContainerSymbol> BuildLocals(
        SyntaxList<StatementSyntax> statements,
        Binder enclosingBinder) {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance(DefaultLocalSymbolArrayCapacity);
        foreach (var statement in statements)
            BuildLocals(enclosingBinder, statement, locals);

        return locals.ToImmutableAndFree();
    }

    internal void BuildLocals(
        Binder enclosingBinder,
        StatementSyntax statement,
        ArrayBuilder<DataContainerSymbol> locals) {
        var innerStatement = statement;

        switch (innerStatement.kind) {
            case SyntaxKind.LocalDeclarationStatement: {
                    var localDeclarationBinder = enclosingBinder.GetBinder(innerStatement) ?? enclosingBinder;
                    var decl = (LocalDeclarationStatementSyntax)innerStatement;

                    decl.declaration.type.VisitRankSpecifiers((rankSpecifier, args) => {
                        FindExpressionVariablesInRankSpecifier(rankSpecifier.size, args);
                    }, (localScopeBinder: this, locals, localDeclarationBinder));

                    DataContainerDeclarationKind kind;

                    if (decl.isConst)
                        kind = DataContainerDeclarationKind.Constant;
                    else if (decl.isConstExpr)
                        kind = DataContainerDeclarationKind.ConstantExpression;
                    else
                        kind = DataContainerDeclarationKind.Variable;

                    var localSymbol = MakeLocal(decl.declaration, kind, localDeclarationBinder);
                    locals.Add(localSymbol);

                    ExpressionVariableFinder.FindExpressionVariables(
                        this,
                        locals,
                        decl.declaration,
                        localDeclarationBinder
                    );
                }
                break;
            case SyntaxKind.LocalFunctionStatement: {
                    var localFunctionDeclarationBinder = enclosingBinder.GetBinder(innerStatement) ?? enclosingBinder;
                    var decl = (LocalFunctionStatementSyntax)innerStatement;

                    foreach (var parameter in decl.parameterList.parameters) {
                        parameter.type?.VisitRankSpecifiers((rankSpecifier, args) => {
                            FindExpressionVariablesInRankSpecifier(rankSpecifier.size, args);
                        }, (localScopeBinder: this, locals, localDeclarationBinder: localFunctionDeclarationBinder));
                    }

                    if (decl.constraintClauseList is not null) {
                        foreach (var constraintClause in decl.constraintClauseList.constraintClauses) {
                            constraintClause.extendConstraint?.type.VisitRankSpecifiers((rankSpecifier, args) => {
                                FindExpressionVariablesInRankSpecifier(rankSpecifier.size, args);
                            }, (
                                localScopeBinder: this,
                                locals,
                                localDeclarationBinder: localFunctionDeclarationBinder
                            ));
                        }
                    }
                }
                break;
            case SyntaxKind.ExpressionStatement:
            case SyntaxKind.IfStatement:
            case SyntaxKind.ReturnStatement:
                ExpressionVariableFinder.FindExpressionVariables(
                    this,
                    locals,
                    innerStatement,
                    enclosingBinder.GetBinder(innerStatement) ?? enclosingBinder
                );

                break;
            default:
                break;
        }

        static void FindExpressionVariablesInRankSpecifier(
            ExpressionSyntax expression,
            (LocalScopeBinder localScopeBinder, ArrayBuilder<DataContainerSymbol> locals, Binder localDeclarationBinder) args) {
            ExpressionVariableFinder.FindExpressionVariables(
                args.localScopeBinder,
                args.locals,
                expression,
                args.localDeclarationBinder
            );
        }
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

    private protected SourceDataContainerSymbol MakeLocal(
        VariableDeclarationSyntax declaration,
        DataContainerDeclarationKind kind,
        Binder initializerBinder = null) {
        return SourceDataContainerSymbol.MakeLocal(
            containingMember,
            this,
            allowRefKind: true,
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

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken identifier) {
        if (_localsMap is not null && _localsMap.TryGetValue(identifier.text, out var result)) {
            if (result.identifierToken == identifier)
                return (SourceDataContainerSymbol)result;

            foreach (var local in locals) {
                if (local.identifierToken == identifier)
                    return (SourceDataContainerSymbol)local;
            }
        }

        return base.LookupLocal(identifier);
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        if (_localFunctionsMap is not null && _localFunctionsMap.TryGetValue(identifier.text, out var result)) {
            if (result.identifier == identifier)
                return result;

            foreach (var local in _localFunctions) {
                if (local.identifier == identifier)
                    return local;
            }
        }

        return base.LookupLocalFunction(identifier);
    }

    internal virtual bool EnsureSingleDefinition(
        Symbol symbol,
        string name,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        DataContainerSymbol existingLocal = null;
        LocalFunctionSymbol existingLocalFunction = null;

        var localsMap = _localsMap;
        var localFunctionsMap = _localFunctionsMap;

        if ((localsMap is not null && localsMap.TryGetValue(name, out existingLocal)) ||
            (localFunctionsMap is not null && localFunctionsMap.TryGetValue(name, out existingLocalFunction))) {
            var existingSymbol = (Symbol)existingLocal ?? existingLocalFunction;
            if (symbol == existingSymbol)
                return false;

            return ReportConflictWithLocal(existingSymbol, symbol, name, location, diagnostics);
        }

        return false;
    }

    private bool ReportConflictWithLocal(
        Symbol local,
        Symbol newSymbol,
        string name,
        TextLocation newLocation,
        BelteDiagnosticQueue diagnostics) {
        var newSymbolKind = newSymbol is null ? SymbolKind.Parameter : newSymbol.kind;

        if (newSymbolKind == SymbolKind.ErrorType)
            return true;

        var declaredInThisScope = false;

        declaredInThisScope |= newSymbolKind == SymbolKind.Local && locals.Contains((DataContainerSymbol)newSymbol);
        declaredInThisScope |= newSymbolKind == SymbolKind.Method &&
            localFunctions.Contains((LocalFunctionSymbol)newSymbol);

        if (declaredInThisScope && newLocation.span.start >= local.location.span.start) {
            diagnostics.Push(Error.LocalAlreadyDeclared(newLocation, name));
            return true;
        }

        switch (newSymbolKind) {
            case SymbolKind.Local:
            case SymbolKind.Parameter:
            case SymbolKind.Method:
            case SymbolKind.TemplateParameter:
                diagnostics.Push(Error.LocalShadowsParameter(newLocation, name));
                return true;
        }

        diagnostics.Push(Error.InternalError(newLocation));
        return false;
    }

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        var localsMap = _localsMap;

        if (localsMap is not null) {
            if (localsMap.TryGetValue(name, out var localSymbol)) {
                result.MergeEqual(originalBinder.CheckViability(
                    localSymbol,
                    arity,
                    options,
                    null,
                    diagnose,
                    basesBeingResolved
                ));
            }
        }

        var localFunctionsMap = _localFunctionsMap;

        if (localFunctionsMap is not null && options.CanConsiderLocals()) {
            if (localFunctionsMap.TryGetValue(name, out var localSymbol)) {
                result.MergeEqual(originalBinder.CheckViability(
                    localSymbol,
                    arity,
                    options,
                    null,
                    diagnose,
                    basesBeingResolved
                ));
            }
        }
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        if (options.CanConsiderLocals()) {
            if (_localsMap is not null) {
                foreach (var local in _localsMap) {
                    if (originalBinder.CanAddLookupSymbolInfo(local.Value, options, result, null))
                        result.AddSymbol(local.Value, local.Key, 0);
                }
            }

            if (_localFunctionsMap is not null) {
                foreach (var local in _localFunctionsMap) {
                    if (originalBinder.CanAddLookupSymbolInfo(local.Value, options, result, null))
                        result.AddSymbol(local.Value, local.Key, 0);
                }
            }
        }
    }
}
