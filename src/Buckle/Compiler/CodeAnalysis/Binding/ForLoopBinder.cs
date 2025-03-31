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
        ExpressionVariableFinder.FindExpressionVariables(this, locals, _syntax.initializer);
        return locals.ToImmutableAndFree();
    }

    internal override BoundForStatement BindForParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        var result = BindForParts(_syntax, originalBinder, diagnostics);
        return result;
    }

    private BoundForStatement BindForParts(ForStatementSyntax node, Binder originalBinder, BelteDiagnosticQueue diagnostics) {
        // var initializer = originalBinder.BindStatementExpressionList(node.initializers, diagnostics);
        var initializer = originalBinder.BindStatement(node.initializer, diagnostics);
        BoundExpression condition = null;
        var innerLocals = ImmutableArray<DataContainerSymbol>.Empty;
        var conditionSyntax = node.condition;

        if (conditionSyntax is not null) {
            originalBinder = originalBinder.GetBinder(conditionSyntax);
            condition = originalBinder.BindBooleanExpression(conditionSyntax, diagnostics);
            innerLocals = originalBinder.GetDeclaredLocalsForScope(conditionSyntax);
        }

        BoundExpression increment = null;
        // SeparatedSyntaxList<ExpressionSyntax> incrementors = node.step;
        // if (incrementors.Count > 0) {
        //     var scopeDesignator = incrementors.First();
        //     var incrementBinder = originalBinder.GetBinder(scopeDesignator);
        //     increment = incrementBinder.BindStatementExpressionList(incrementors, diagnostics);
        //     Debug.Assert(increment.Kind != BoundKind.StatementList || ((BoundStatementList)increment).Statements.Length > 1);

        //     var locals = incrementBinder.GetDeclaredLocalsForScope(scopeDesignator);
        //     if (!locals.IsEmpty) {
        //         if (increment.Kind == BoundKind.StatementList) {
        //             increment = new BoundBlock(scopeDesignator, locals, ((BoundStatementList)increment).Statements) { WasCompilerGenerated = true };
        //         } else {
        //             increment = new BoundBlock(increment.Syntax, locals, ImmutableArray.Create(increment)) { WasCompilerGenerated = true };
        //         }
        //     }
        // }
        if (node.step is not null) {
            var scopeDesignator = node.step;
            var incrementBinder = originalBinder.GetBinder(scopeDesignator);
            increment = incrementBinder.BindExpression(node.step, diagnostics);

            var locals = incrementBinder.GetDeclaredLocalsForScope(scopeDesignator);

            if (!locals.IsEmpty) {
                // increment = new BoundBlock(increment.Syntax, locals, ImmutableArray.Create(increment)) { WasCompilerGenerated = true };
            }
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
