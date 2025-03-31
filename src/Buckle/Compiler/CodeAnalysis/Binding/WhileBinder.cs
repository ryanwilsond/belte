using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class WhileBinder : LoopBinder {
    private readonly StatementSyntax _syntax;

    internal WhileBinder(Binder enclosing, StatementSyntax syntax)
        : base(enclosing) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    internal override BoundWhileStatement BindWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        var node = (WhileStatementSyntax)_syntax;
        var condition = originalBinder.BindBooleanExpression(node.condition, diagnostics);
        var body = originalBinder.BindPossibleEmbeddedStatement(node.body, diagnostics); ;
        return new BoundWhileStatement(node, locals, condition, body, breakLabel, continueLabel);
    }

    internal override BoundDoWhileStatement BindDoWhileParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        var node = (DoWhileStatementSyntax)_syntax;
        var condition = originalBinder.BindBooleanExpression(node.condition, diagnostics);
        var body = originalBinder.BindPossibleEmbeddedStatement(node.body, diagnostics);
        return new BoundDoWhileStatement(node, locals, condition, body, breakLabel, continueLabel);
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

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance();
        ExpressionSyntax condition;

        switch (_syntax.kind) {
            case SyntaxKind.WhileStatement:
                condition = ((WhileStatementSyntax)_syntax).condition;
                break;
            case SyntaxKind.DoWhileStatement:
                condition = ((DoWhileStatementSyntax)_syntax).condition;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(_syntax.kind);
        }

        ExpressionVariableFinder.FindExpressionVariables(this, locals, node: condition);
        return locals.ToImmutableAndFree();
    }
}
