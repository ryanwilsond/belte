using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ExpressionFieldFinder : ExpressionVariableFinder<Symbol> {
    private static readonly ObjectPool<ExpressionFieldFinder> PoolInstance = CreatePool();

    public static ObjectPool<ExpressionFieldFinder> CreatePool() {
        return new ObjectPool<ExpressionFieldFinder>(() => new ExpressionFieldFinder(), 10);
    }

    private SourceMemberContainerTypeSymbol _containingType;
    private DeclarationModifiers _modifiers;
    private FieldSymbol _containingField;

    internal static void FindExpressionVariables(
        ArrayBuilder<Symbol> builder,
        BelteSyntaxNode node,
        SourceMemberContainerTypeSymbol containingType,
        DeclarationModifiers modifiers,
        FieldSymbol containingFieldOpt) {
        if (node is null)
            return;

        var finder = PoolInstance.Allocate();
        finder._containingType = containingType;
        finder._modifiers = modifiers;
        finder._containingField = containingFieldOpt;

        finder.FindExpressionVariables(builder, node);

        finder._containingType = null;
        finder._modifiers = DeclarationModifiers.None;
        finder._containingField = null;
        PoolInstance.Free(finder);
    }

    private protected override Symbol MakeDeclarationExpressionVariable(
        VariableDeclarationSyntax node,
        SyntaxToken identifier,
        ArgumentListSyntax argumentListSyntax,
        SyntaxTokenList modifiers,
        SyntaxNode nodeToBind) {
        // TODO Check if this should be a GlobalExpressionVariable
        return SourceDataContainerSymbol.MakeLocal(
            containingSymbol: _containingType,
            scopeBinder: null,
            allowRefKind: true,
            initializer: null,
            nodeBinder: null,
            typeSyntax: node.type,
            identifierToken: identifier,
            declarationKind: DataContainerDeclarationKind.Variable,
            modifiers: modifiers,
            nodeToBind: nodeToBind
        );
    }
}
