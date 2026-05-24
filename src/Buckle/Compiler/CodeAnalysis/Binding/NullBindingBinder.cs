using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class NullBindingBinder : LocalScopeBinder {
    private readonly NullBindingStatementSyntax _syntax;
    private SourceDataContainerSymbol _valueSymbol;

    internal NullBindingBinder(Binder enclosing, NullBindingStatementSyntax syntax)
        : base(enclosing) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance();

        _valueSymbol = SourceDataContainerSymbol.MakeDeconstructionLocal(
            containingMember,
            this,
            this,
            null,
            _syntax.target,
            DataContainerDeclarationKind.NullBindingLocal,
            _syntax
        );

        locals.Add(_valueSymbol);

        return locals.ToImmutableAndFree();
    }

    internal override BoundNullBindingStatement BindNullBindingParts(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        return BindNullBindingParts(_syntax, originalBinder, diagnostics);
    }

    internal override BoundStatement BindNullBindingDeconstruction(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        var sourceExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        BindNullBindingSource(
            _syntax,
            _syntax.expression,
            ref sourceExpr,
            diagnostics,
            out var inferredType
        );

        _valueSymbol.SetTypeWithAnnotations(inferredType);

        return new BoundExpressionStatement(_syntax, BoundFactory.Local(_syntax, _valueSymbol));
    }

    private BoundNullBindingStatement BindNullBindingParts(
        NullBindingStatementSyntax node,
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics) {
        _ = locals;

        var sourceExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        BindNullBindingSource(
            _syntax,
            _syntax.expression,
            ref sourceExpr,
            diagnostics,
            out var inferredType
        );

        _valueSymbol.SetTypeWithAnnotations(inferredType);

        var then = originalBinder.BindPossibleEmbeddedStatement(node.then, diagnostics);
        var alternative = (node.elseClause is null)
            ? null
            : originalBinder.BindPossibleEmbeddedStatement(node.elseClause.body, diagnostics);

        return new BoundNullBindingStatement(
            node,
            locals,
            sourceExpr,
            [],
            _valueSymbol,
            then,
            alternative
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
}
