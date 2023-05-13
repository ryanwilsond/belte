
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract partial class SyntaxVisitor<TResult> {
    internal virtual TResult Visit(BelteSyntaxNode node) {
        if (node == null)
            return default(TResult);

        return node.Accept(this);
    }

    internal virtual TResult VisitToken(SyntaxToken token) {
        return DefaultVisit(token);
    }

    internal virtual TResult VisitTrivia(SyntaxTrivia trivia) {
        return DefaultVisit(trivia);
    }

    protected virtual TResult DefaultVisit(BelteSyntaxNode node) {
        return default(TResult);
    }
}
