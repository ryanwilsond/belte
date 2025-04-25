using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class SimpleProgramBinder : LocalScopeBinder {
    private readonly SynthesizedEntryPoint _entryPoint;

    internal SimpleProgramBinder(Binder enclosing, SynthesizedEntryPoint entryPoint)
        : base(enclosing, enclosing.flags) {
        _entryPoint = entryPoint;
    }

    internal override bool isLocalFunctionsScopeBinder => true;

    internal override bool isLabelsScopeBinder => true;

    internal override SyntaxNode scopeDesignator => _entryPoint.syntaxNode;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return localFunctions;

        throw ExceptionUtilities.Unreachable();
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance(DefaultLocalSymbolArrayCapacity);

        foreach (var statement in _entryPoint.compilationUnit.members) {
            if (statement is GlobalStatementSyntax topLevelStatement)
                BuildLocals(this, topLevelStatement.statement, locals);
        }

        for (var entryPoint = _entryPoint.declaringCompilation.previous.entryPoint;
            entryPoint is SynthesizedEntryPoint m;
            entryPoint = entryPoint.declaringCompilation.previous.entryPoint) {
            var entryPointBinder = m.TryGetBodyBinder().GetBinder(m.compilationUnit);
            locals.AddRange(entryPointBinder.GetDeclaredLocalsForScope(m.compilationUnit));
        }

        return locals.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        var locals = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

        foreach (var statement in _entryPoint.compilationUnit.members) {
            if (statement is GlobalStatementSyntax topLevelStatement)
                BuildLocalFunctions(topLevelStatement.statement, ref locals);
        }

        for (var entryPoint = _entryPoint.declaringCompilation.previous.entryPoint;
            entryPoint is SynthesizedEntryPoint m;
            entryPoint = entryPoint.declaringCompilation.previous.entryPoint) {
            var entryPointBinder = m.TryGetBodyBinder().GetBinder(m.compilationUnit);
            locals.AddRange(entryPointBinder.GetDeclaredLocalFunctionsForScope(m.compilationUnit));
        }

        return locals.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LabelSymbol> BuildLabels() {
        ArrayBuilder<LabelSymbol>? labels = null;

        foreach (var statement in _entryPoint.compilationUnit.members) {
            if (statement is GlobalStatementSyntax topLevelStatement)
                BuildLabels(_entryPoint, topLevelStatement.statement, ref labels);
        }

        return labels?.ToImmutableAndFree() ?? [];
    }
}
