
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxReference {
    internal SyntaxReference(SyntaxNode node) {
        this.node = node;
    }

    internal SyntaxNode node { get; }

    internal SyntaxTree syntaxTree => node.syntaxTree;

    internal TextSpan span => node.span;

    internal TextLocation location => node.location;
}
