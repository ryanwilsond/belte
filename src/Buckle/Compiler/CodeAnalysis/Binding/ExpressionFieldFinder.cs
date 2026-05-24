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

    private protected override Symbol MakePatternVariable(
        TypeSyntax type,
        DeclarationPatternSyntax node,
        SyntaxNode nodeToBind) {
        return GlobalExpressionVariable.Create(
            _containingType,
            _modifiers,
            type,
            node.identifier.text,
            node,
            node.location,
            _containingField,
            nodeToBind
        );
    }

    private protected override Symbol MakeDeclarationExpressionVariable(
        DeclarationExpressionSyntax node,
        SyntaxToken identifier,
        BaseArgumentListSyntax argumentListSyntax,
        SyntaxTokenList modifiers,
        SyntaxNode nodeToBind) {
        return GlobalExpressionVariable.Create(
            _containingType,
            _modifiers,
            node.type,
            node.identifier.text,
            node,
            node.identifier.location,
            _containingField,
            nodeToBind
        );
    }
}
