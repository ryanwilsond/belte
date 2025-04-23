using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class SimpleProgramUnitBinder : LocalScopeBinder {
    private readonly SimpleProgramBinder _scope;

    internal SimpleProgramUnitBinder(Binder enclosing, SimpleProgramBinder scope) : base(enclosing, enclosing.flags) {
        _scope = scope;
    }

    internal override bool isLocalFunctionsScopeBinder => _scope.isLocalFunctionsScopeBinder;

    internal override bool isLabelsScopeBinder => false;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        return _scope.GetDeclaredLocalsForScope(scopeDesignator);
    }

    internal override SyntaxNode scopeDesignator => _scope.scopeDesignator;

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        return _scope.GetDeclaredLocalFunctionsForScope(scopeDesignator);
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        return _scope.locals;
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        return _scope.localFunctions;
    }

    private protected override ImmutableArray<LabelSymbol> BuildLabels() {
        return [];
    }
}
