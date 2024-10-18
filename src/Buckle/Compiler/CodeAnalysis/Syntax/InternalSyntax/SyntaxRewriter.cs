
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxRewriter : SyntaxVisitor<BelteSyntaxNode> {
    private protected readonly bool _visitIntoTrivia;

    internal SyntaxRewriter(bool visitIntoTrivia = false) {
        _visitIntoTrivia = visitIntoTrivia;
    }

    internal override BelteSyntaxNode VisitToken(SyntaxToken token) {
        var leading = VisitList(token.leadingTrivia);
        var trailing = VisitList(token.trailingTrivia);

        if (leading != token.leadingTrivia || trailing != token.trailingTrivia) {
            if (leading != token.leadingTrivia)
                token = token.TokenWithLeadingTrivia(leading.node);

            if (trailing != token.trailingTrivia)
                token = token.TokenWithTrailingTrivia(trailing.node);
        }

        return token;
    }

    internal SyntaxList<T> VisitList<T>(SyntaxList<T> list) where T : BelteSyntaxNode {
        SyntaxListBuilder alternate = null;

        for (int i = 0, n = list.Count; i < n; i++) {
            var item = list[i];
            var visited = Visit(item);

            if (item != visited && alternate is null) {
                alternate = new SyntaxListBuilder(n);
                alternate.AddRange(list, 0, i);
            }

            alternate?.Add(visited);
        }

        if (alternate is not null)
            return alternate.ToList();

        return list;
    }

    internal SeparatedSyntaxList<T> VisitList<T>(SeparatedSyntaxList<T> list) where T : BelteSyntaxNode {
        var withSeparators = (SyntaxList<BelteSyntaxNode>)list.GetWithSeparators();
        var result = VisitList(withSeparators);

        if (result != withSeparators)
            return result.AsSeparatedList<T>();

        return list;
    }
}
