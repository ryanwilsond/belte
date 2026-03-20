using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class EmbeddedStatementBinder : LocalScopeBinder {
    private readonly StatementSyntax _statement;

    internal EmbeddedStatementBinder(Binder enclosing, StatementSyntax statement) : base(enclosing, enclosing.flags) {
        _statement = statement;
    }

    internal override bool isLocalFunctionsScopeBinder => true;

    internal override bool isLabelsScopeBinder => true;

    internal override SyntaxNode scopeDesignator => _statement;

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance(DefaultLocalSymbolArrayCapacity);
        BuildLocals(this, _statement, locals);
        return locals.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        ArrayBuilder<LocalFunctionSymbol> locals = null;
        BuildLocalFunctions(_statement, ref locals);
        return locals?.ToImmutableAndFree() ?? [];
    }

    private protected override ImmutableArray<LabelSymbol> BuildLabels() {
        ArrayBuilder<LabelSymbol> labels = null;
        var containingMethod = (MethodSymbol)containingMember;
        BuildLabels(containingMethod, _statement, ref labels);
        return labels?.ToImmutableAndFree() ?? [];
    }

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(BelteSyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return localFunctions;

        throw ExceptionUtilities.Unreachable();
    }
}
