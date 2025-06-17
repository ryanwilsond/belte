using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal class SyntaxReference {
    private protected SyntaxReference() { }

    internal SyntaxReference(SyntaxNode node) {
        this.node = node;
    }

    internal virtual SyntaxNode node { get; }

    internal virtual SyntaxTree syntaxTree => node.syntaxTree;

    internal virtual TextSpan span => node.span;

    internal TextLocation location => node.location;
}
