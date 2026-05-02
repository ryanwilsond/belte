using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ForEachLoopBinder : LoopBinder {
    private readonly ForEachStatementSyntax _syntax;
    private SourceDataContainerSymbol _valueSymbol;
    private SourceDataContainerSymbol _indexSymbol;

    internal ForEachLoopBinder(Binder enclosing, ForEachStatementSyntax syntax)
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
            _syntax.valueIdentifier,
            DataContainerDeclarationKind.ForEachLocal,
            _syntax
        );

        locals.Add(_valueSymbol);

        if (_syntax.indexIdentifier is not null) {
            _indexSymbol = SourceDataContainerSymbol.MakeDeconstructionLocal(
                containingMember,
                this,
                this,
                null,
                _syntax.indexIdentifier,
                DataContainerDeclarationKind.ForEachLocal,
                _syntax
            );

            locals.Add(_indexSymbol);
        }

        return locals.ToImmutableAndFree();
    }

    internal override BoundForEachStatement BindForEachParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return BindForEachParts(_syntax, originalBinder, diagnostics);
    }

    internal override BoundStatement BindForEachDeconstruction(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        var collectionExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        BindForEachCollection(
            _syntax,
            _syntax.expression,
            ref collectionExpr,
            diagnostics,
            out var inferredType
        );

        _valueSymbol.SetTypeWithAnnotations(inferredType);
        _indexSymbol?.SetTypeWithAnnotations(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int)));

        return new BoundExpressionStatement(_syntax, BoundFactory.Local(_syntax, _valueSymbol));
    }

    private BoundForEachStatement BindForEachParts(
        ForEachStatementSyntax node,
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics) {
        _ = locals;

        var collectionExpr = originalBinder.GetBinder(_syntax.expression)
            .BindRValueWithoutTargetType(_syntax.expression, diagnostics);

        BindForEachCollection(
            _syntax,
            _syntax.expression,
            ref collectionExpr,
            diagnostics,
            out var inferredType
        );

        _valueSymbol.SetTypeWithAnnotations(inferredType);
        _indexSymbol?.SetTypeWithAnnotations(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int)));

        var body = originalBinder.BindPossibleEmbeddedStatement(node.body, diagnostics);

        return new BoundForEachStatement(
            node,
            locals,
            collectionExpr,
            [],
            _valueSymbol,
            _indexSymbol,
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
}
