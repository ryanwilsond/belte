using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class UsingStatementBinder : LocalScopeBinder {
    private readonly UsingStatementSyntax _syntax;

    internal UsingStatementBinder(Binder enclosing, UsingStatementSyntax syntax) : base(enclosing) {
        _syntax = syntax;
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var declarationSyntax = _syntax.declaration;

        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance(1);

        declarationSyntax.type.VisitRankSpecifiers((rankSpecifier, args) => {
            ExpressionVariableFinder.FindExpressionVariables(args.binder, args.locals, rankSpecifier.size);
        }, (binder: this, locals: locals));

        locals.Add(MakeLocal(declarationSyntax, null));
        ExpressionVariableFinder.FindExpressionVariables(this, locals, declarationSyntax);

        return locals.ToImmutableAndFree();
    }

    internal override BoundStatement BindUsingStatementParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        var declarationSyntax = _syntax;
        var boundUsingStatement = BindUsingStatementOrDeclarationFromParts(
            declarationSyntax,
            _syntax.usingKeyword,
            originalBinder,
            this,
            diagnostics
        );

        return boundUsingStatement;
    }

    internal static BoundStatement BindUsingStatementOrDeclarationFromParts(
        UsingStatementSyntax syntax,
        SyntaxToken usingKeyword,
        Binder originalBinder,
        UsingStatementBinder usingBinderOpt,
        BelteDiagnosticQueue diagnostics) {
        var typeSyntax = syntax.declaration.type.SkipRef(out _);
        var isConst = false;
        var isConstExpr = false;

        var declarationType = originalBinder.BindVariableTypeWithAnnotations(
            syntax.declaration,
            diagnostics,
            typeSyntax,
            ref isConst,
            ref isConstExpr,
            out var isImplicitlyTyped,
            out var isNonNullable,
            out var isNullable,
            out var alias
        );

        var kind = isConstExpr
            ? DataContainerDeclarationKind.ConstantExpression
            : (isConst ? DataContainerDeclarationKind.Constant : DataContainerDeclarationKind.Variable);

        var declaration = originalBinder.BindVariableDeclaration(
            kind,
            isImplicitlyTyped,
            isNonNullable,
            isNullable,
            syntax.declaration,
            typeSyntax,
            declarationType,
            alias,
            diagnostics,
            true,
            null,
            true,
            syntax
        );

        var body = usingBinderOpt.BindPossibleEmbeddedStatement(syntax.statement, diagnostics);

        return new BoundUsingStatement(syntax, declaration, body);
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
