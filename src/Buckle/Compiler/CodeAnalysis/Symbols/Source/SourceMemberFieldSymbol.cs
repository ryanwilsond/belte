using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMemberFieldSymbol : SourceFieldSymbolWithSyntaxReference {
    internal SourceMemberFieldSymbol(
        SourceMemberContainerTypeSymbol containingType,
        DeclarationModifiers modifiers,
        string name,
        SyntaxReference syntaxReference)
        : base(containingType, name, syntaxReference) {
        _modifiers = modifiers;
    }

    private protected sealed override DeclarationModifiers _modifiers { get; }

    private protected abstract TypeSyntax _typeSyntax { get; }

    private protected abstract SyntaxTokenList _modifiersTokenList { get; }

    private protected TypeChecks()
}
