
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract partial class SyntaxVisitor {
    internal virtual void Visit(BelteSyntaxNode node) {
        if (node == null)
            return;

        node.Accept(this);
    }

    internal virtual void VisitToken(SyntaxToken token) {
        DefaultVisit(token);
    }

    internal virtual void VisitTrivia(SyntaxTrivia trivia) {
        DefaultVisit(trivia);
    }

    internal virtual void DefaultVisit(BelteSyntaxNode node) { }
}
