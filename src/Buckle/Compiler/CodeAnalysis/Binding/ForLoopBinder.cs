using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ForLoopBinder : LoopBinder {
    private readonly ForStatementSyntax _syntax;

    internal ForLoopBinder(Binder enclosing, ForStatementSyntax syntax)
        : base(enclosing) {
        _syntax = syntax;
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance();
        BuildLocals(this, _syntax.initializer, locals);
        return locals.ToImmutableAndFree();
    }

    internal override BoundForStatement BindForParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return BindForParts(_syntax, originalBinder, diagnostics);
    }

    private BoundForStatement BindForParts(ForStatementSyntax node, Binder originalBinder, BelteDiagnosticQueue diagnostics) {
        var initializer = originalBinder.BindStatement(node.initializer, diagnostics);
        BoundExpression condition = null;
        var innerLocals = ImmutableArray<DataContainerSymbol>.Empty;
        var conditionSyntax = node.condition;

        if (conditionSyntax is not null) {
            originalBinder = originalBinder.GetBinder(conditionSyntax);
            condition = originalBinder.BindBooleanExpression(conditionSyntax, diagnostics);
            innerLocals = originalBinder.GetDeclaredLocalsForScope(conditionSyntax);
        }

        BoundStatement increment = null;

        if (node.step is not null) {
            var scopeDesignator = node.step;
            var incrementBinder = originalBinder.GetBinder(scopeDesignator);
            increment = new BoundExpressionStatement(node.step, incrementBinder.BindExpression(node.step, diagnostics));

            var locals = incrementBinder.GetDeclaredLocalsForScope(scopeDesignator);

            if (!locals.IsEmpty)
                increment = new BoundBlockStatement(increment.syntax, [increment], locals, []);
        }

        var body = originalBinder.BindPossibleEmbeddedStatement(node.body, diagnostics);

        return new BoundForStatement(
            node,
            locals,
            initializer,
            innerLocals,
            condition,
            increment,
            body,
            breakLabel,
            continueLabel
        );
    }

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (_syntax == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override SyntaxNode scopeDesignator => _syntax;
}
