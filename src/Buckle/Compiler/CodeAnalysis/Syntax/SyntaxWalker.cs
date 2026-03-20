using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class SyntaxWalker : SyntaxVisitor {
    private int _recursionDepth;

    private protected SyntaxWalker(SyntaxWalkerDepth depth = SyntaxWalkerDepth.Node) {
        _depth = depth;
    }

    private protected SyntaxWalkerDepth _depth { get; }

    internal override void Visit(SyntaxNode node) {
        if (node is not null) {
            _recursionDepth++;
            StackGuard.EnsureSufficientExecutionStack(_recursionDepth);
            ((BelteSyntaxNode)node).Accept(this);
            _recursionDepth--;
        }
    }

    internal override void DefaultVisit(SyntaxNode node) {
        var childCnt = node.ChildNodesAndTokens().Count;
        var i = 0;
        var slotData = new ChildSyntaxList.SlotData(node);

        do {
            var child = ChildSyntaxList.ItemInternal((BelteSyntaxNode)node, i, ref slotData);
            i++;

            var asNode = child.AsNode();

            if (asNode is not null) {
                if (_depth >= SyntaxWalkerDepth.Node)
                    Visit(asNode);
            } else {
                if (_depth >= SyntaxWalkerDepth.Token)
                    VisitToken(child.AsToken());
            }
        } while (i < childCnt);
    }

    internal virtual void VisitToken(SyntaxToken token) {
        if (_depth >= SyntaxWalkerDepth.Trivia) {
            VisitLeadingTrivia(token);
            VisitTrailingTrivia(token);
        }
    }

    internal virtual void VisitLeadingTrivia(SyntaxToken token) {
        if (token.hasLeadingTrivia) {
            foreach (var tr in token.leadingTrivia)
                VisitTrivia(tr);
        }
    }

    internal virtual void VisitTrailingTrivia(SyntaxToken token) {
        if (token.hasTrailingTrivia) {
            foreach (var tr in token.trailingTrivia)
                VisitTrivia(tr);
        }
    }

    internal virtual void VisitTrivia(SyntaxTrivia trivia) { }
}
