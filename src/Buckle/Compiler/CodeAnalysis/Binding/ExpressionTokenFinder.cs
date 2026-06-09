using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ExpressionTokenFinder : SyntaxWalker {
    private static readonly ObjectPool<ExpressionTokenFinder> PoolInstance = CreatePool();

    private ArrayBuilder<TokenSymbol> _tokensBuilder;
    private Binder _scopeBinder;
    private Binder _enclosingBinder;

    public static ObjectPool<ExpressionTokenFinder> CreatePool() {
        return new ObjectPool<ExpressionTokenFinder>(() => new ExpressionTokenFinder(), 10);
    }

    internal static void FindExpressionTokens(
        Binder scopeBinder,
        ref ArrayBuilder<TokenSymbol> builder,
        BelteSyntaxNode node,
        Binder enclosingBinderOpt = null) {
        if (node is null)
            return;

        var finder = PoolInstance.Allocate();
        finder._scopeBinder = scopeBinder;
        finder._enclosingBinder = enclosingBinderOpt ?? scopeBinder;

        finder.FindExpressionTokens(ref builder, node);

        finder._scopeBinder = null;
        finder._enclosingBinder = null;
        PoolInstance.Free(finder);
    }

    private void FindExpressionTokens(ref ArrayBuilder<TokenSymbol> builder, BelteSyntaxNode node) {
        var save = _tokensBuilder;
        _tokensBuilder = builder;
        Visit(node);
        builder = _tokensBuilder;
        _tokensBuilder = save;
    }

    internal override void VisitReversibleExpression(ReversibleExpressionSyntax node) {
        var token = new SourceTokenSymbol(_scopeBinder.containingMember, node.identifier, _scopeBinder);
        _tokensBuilder ??= ArrayBuilder<TokenSymbol>.GetInstance();
        _tokensBuilder.Add(token);

        base.VisitReversibleExpression(node);
    }
}
