using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class CatchClauseBinder : LocalScopeBinder {
    private readonly CatchClauseSyntax _syntax;

    internal CatchClauseBinder(Binder enclosing, CatchClauseSyntax syntax)
        : base(enclosing, (enclosing.flags | BinderFlags.InCatchBlock) & ~BinderFlags.InNestedFinallyBlock) {
        _syntax = syntax;
    }

    internal override SyntaxNode scopeDesignator => _syntax;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (_syntax == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<TokenSymbol> GetDeclaredTokensForScope(SyntaxNode scopeDesignator) {
        if (_syntax == scopeDesignator)
            return tokens;

        throw ExceptionUtilities.Unreachable();
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var locals = ArrayBuilder<DataContainerSymbol>.GetInstance();

        var declarationOpt = _syntax.declaration;

        if (declarationOpt is not null && declarationOpt.identifier is not null) {
            locals.Add(SourceDataContainerSymbol.MakeLocal(
                containingMember,
                this,
                allowRefKind: false,
                declarationOpt.type,
                declarationOpt.identifier,
                null,
                SyntaxTokenList.Empty,
                DataContainerDeclarationKind.CatchVariable
            ));
        }

        return locals.ToImmutableAndFree();
    }
}
