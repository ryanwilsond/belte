using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ExpressionVariableBinder : LocalScopeBinder {
    internal override SyntaxNode scopeDesignator { get; }

    internal ExpressionVariableBinder(SyntaxNode scopeDesignator, Binder next) : base(next) {
        this.scopeDesignator = scopeDesignator;
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var builder = ArrayBuilder<DataContainerSymbol>.GetInstance();
        var scopeDesignator = (BelteSyntaxNode)this.scopeDesignator;

        ExpressionVariableFinder.FindExpressionVariables(
            this,
            builder,
            scopeDesignator,
            GetBinder(scopeDesignator)
        );

        return builder.ToImmutableAndFree();
    }

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (this.scopeDesignator == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }
}
