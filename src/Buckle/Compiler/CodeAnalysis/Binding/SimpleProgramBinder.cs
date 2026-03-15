using System.Collections.Generic;
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
        var locals = new HashSet<DataContainerSymbol>();
        var localsBuilder = ArrayBuilder<DataContainerSymbol>.GetInstance(DefaultLocalSymbolArrayCapacity);

        foreach (var statement in _entryPoint.compilationUnit.members) {
            if (statement is GlobalStatementSyntax topLevelStatement)
                BuildLocals(this, topLevelStatement.statement, localsBuilder);
        }

        locals.AddAll(localsBuilder);
        localsBuilder.Free();

        for (var compilation = _entryPoint.declaringCompilation.previous;
            compilation is not null;
            compilation = compilation.previous) {
            if (compilation.entryPoint is not SynthesizedEntryPoint synthesizedEntryPoint)
                continue;

            var compilationUnit = synthesizedEntryPoint.compilationUnit;
            var entryPointBinder = synthesizedEntryPoint
                .TryGetBodyBinder(null, flags.Includes(BinderFlags.IgnoreAccessibility))
                .GetBinder(compilationUnit);

            locals.AddAll(entryPointBinder.GetDeclaredLocalsForScope(compilationUnit));
        }

        return locals.ToImmutableArray();
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        var locals = new HashSet<LocalFunctionSymbol>();
        var localsBuilder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

        foreach (var statement in _entryPoint.compilationUnit.members) {
            if (statement is GlobalStatementSyntax topLevelStatement)
                BuildLocalFunctions(topLevelStatement.statement, ref localsBuilder);
        }

        locals.AddAll(localsBuilder);
        localsBuilder.Free();

        for (var compilation = _entryPoint.declaringCompilation.previous;
            compilation is not null;
            compilation = compilation.previous) {
            if (compilation.entryPoint is not SynthesizedEntryPoint synthesizedEntryPoint)
                continue;

            var compilationUnit = synthesizedEntryPoint.compilationUnit;
            var entryPointBinder = synthesizedEntryPoint
                .TryGetBodyBinder(null, flags.Includes(BinderFlags.IgnoreAccessibility))
                .GetBinder(compilationUnit);

            locals.AddAll(entryPointBinder.GetDeclaredLocalFunctionsForScope(compilationUnit));
        }

        return locals.ToImmutableArray();
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
