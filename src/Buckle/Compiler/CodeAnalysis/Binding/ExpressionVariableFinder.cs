using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ExpressionVariableFinder : ExpressionVariableFinder<DataContainerSymbol> {
    private static readonly ObjectPool<ExpressionVariableFinder> PoolInstance = CreatePool();

    private Binder _scopeBinder;
    private Binder _enclosingBinder;

    public static ObjectPool<ExpressionVariableFinder> CreatePool() {
        return new ObjectPool<ExpressionVariableFinder>(() => new ExpressionVariableFinder(), 10);
    }

    internal static void FindExpressionVariables(
        Binder scopeBinder,
        ArrayBuilder<DataContainerSymbol> builder,
        BelteSyntaxNode node,
        Binder enclosingBinderOpt = null) {
        if (node is null)
            return;

        var finder = PoolInstance.Allocate();
        finder._scopeBinder = scopeBinder;
        finder._enclosingBinder = enclosingBinderOpt ?? scopeBinder;

        finder.FindExpressionVariables(builder, node);

        finder._scopeBinder = null;
        finder._enclosingBinder = null;
        PoolInstance.Free(finder);
    }

    internal static void FindExpressionVariables(
        Binder binder,
        ArrayBuilder<DataContainerSymbol> builder,
        SeparatedSyntaxList<ExpressionSyntax> nodes) {
        if (nodes.Count == 0)
            return;

        var finder = PoolInstance.Allocate();
        finder._scopeBinder = binder;
        finder._enclosingBinder = binder;

        finder.FindExpressionVariables(builder, nodes);

        finder._scopeBinder = null;
        finder._enclosingBinder = null;
        PoolInstance.Free(finder);
    }

    private protected override DataContainerSymbol MakeDeclarationExpressionVariable(
        DeclarationExpressionSyntax node,
        SyntaxToken identifier,
        BaseArgumentListSyntax argumentListSyntax,
        SyntaxTokenList modifiers,
        SyntaxNode nodeToBind) {
        return SourceDataContainerSymbol.MakeLocal(
            containingSymbol: _scopeBinder.containingMember,
            scopeBinder: _scopeBinder,
            allowRefKind: true,
            initializer: null,
            nodeBinder: _enclosingBinder,
            typeSyntax: node.type,
            identifierToken: identifier,
            modifiers: modifiers,
            nodeToBind: nodeToBind
        );
    }

    private protected override DataContainerSymbol MakePatternVariable(
        TypeSyntax type,
        DeclarationPatternSyntax node,
        SyntaxNode nodeToBind) {
        // TODO EnclosingContext aware local to prevent duplicates (same for out vars)
        return SourceDataContainerSymbol.MakeLocal(
            containingSymbol: _scopeBinder.containingMember,
            scopeBinder: _scopeBinder,
            allowRefKind: true,
            initializer: null,
            nodeBinder: _enclosingBinder,
            typeSyntax: type,
            identifierToken: node.identifier,
            modifiers: null,
            nodeToBind: nodeToBind
        );
    }
}
