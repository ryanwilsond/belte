
namespace Buckle.CodeAnalysis.Syntax;

internal abstract partial class SyntaxVisitor {
    internal virtual void Visit(SyntaxNode node) {
        if (node is not null)
            ((BelteSyntaxNode)node).Accept(this);
    }

    internal virtual void DefaultVisit(SyntaxNode node) { }
}
