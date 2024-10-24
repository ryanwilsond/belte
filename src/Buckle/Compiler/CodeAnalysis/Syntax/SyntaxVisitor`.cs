
namespace Buckle.CodeAnalysis.Syntax;

internal abstract partial class SyntaxVisitor<TResult> {
    internal virtual TResult Visit(SyntaxNode node) {
        if (node is not null)
            return ((BelteSyntaxNode)node).Accept(this);

        return default;
    }

    internal virtual TResult DefaultVisit(SyntaxNode node) {
        return default;
    }
}
