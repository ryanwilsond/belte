
namespace Buckle.CodeAnalysis.Syntax;

internal sealed class NamespaceDeclarationSyntaxReference : TranslationSyntaxReference {
    internal NamespaceDeclarationSyntaxReference(SyntaxReference reference) : base(reference) { }

    private protected override SyntaxNode Translate(SyntaxReference reference) {
        return GetSyntax(reference);
    }

    internal static SyntaxNode GetSyntax(SyntaxReference reference) {
        var node = (BelteSyntaxNode)reference.node;

        while (node is NameSyntax)
            node = node.parent;

        return node;
    }
}
