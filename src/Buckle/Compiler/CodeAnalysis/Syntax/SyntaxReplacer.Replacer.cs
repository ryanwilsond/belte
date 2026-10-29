using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    private class Replacer<TNode> : SyntaxRewriter where TNode : SyntaxNode {
        private readonly Func<TNode, TNode, SyntaxNode> _computeReplacementNode;
        private readonly Func<SyntaxToken, SyntaxToken, SyntaxToken> _computeReplacementToken;
        private readonly Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> _computeReplacementTrivia;

        private readonly HashSet<SyntaxNode> _nodeSet;
        private readonly HashSet<SyntaxToken> _tokenSet;
        private readonly HashSet<SyntaxTrivia> _triviaSet;
        private readonly HashSet<TextSpan> _spanSet;

        private TextSpan _totalSpan;
        private bool _visitIntoStructuredTrivia;
        private bool _shouldVisitTrivia;

        internal Replacer(
            IEnumerable<TNode> nodes,
            Func<TNode, TNode, SyntaxNode> computeReplacementNode,
            IEnumerable<SyntaxToken> tokens,
            Func<SyntaxToken, SyntaxToken, SyntaxToken> computeReplacementToken,
            IEnumerable<SyntaxTrivia> trivia,
            Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> computeReplacementTrivia) {
            _computeReplacementNode = computeReplacementNode;
            _computeReplacementToken = computeReplacementToken;
            _computeReplacementTrivia = computeReplacementTrivia;

            _nodeSet = nodes != null ? new HashSet<SyntaxNode>(nodes) : NoNodes;
            _tokenSet = tokens != null ? new HashSet<SyntaxToken>(tokens) : NoTokens;
            _triviaSet = trivia != null ? new HashSet<SyntaxTrivia>(trivia) : NoTrivia;

            _spanSet = [];

            CalculateVisitationCriteria();
        }

        private static readonly HashSet<SyntaxNode> NoNodes = [];
        private static readonly HashSet<SyntaxToken> NoTokens = [];
        private static readonly HashSet<SyntaxTrivia> NoTrivia = [];

        internal override bool visitIntoStructuredTrivia => _visitIntoStructuredTrivia;

        internal bool hasWork => _nodeSet.Count + _tokenSet.Count + _triviaSet.Count > 0;

        private void CalculateVisitationCriteria() {
            _spanSet.Clear();

            foreach (var node in _nodeSet)
                _spanSet.Add(node.fullSpan);

            foreach (var token in _tokenSet)
                _spanSet.Add(token.fullSpan);

            foreach (var trivia in _triviaSet)
                _spanSet.Add(trivia.fullSpan);

            var first = true;
            var start = 0;
            var end = 0;

            foreach (var span in _spanSet) {
                if (first) {
                    start = span.start;
                    end = span.end;
                    first = false;
                } else {
                    start = Math.Min(start, span.start);
                    end = Math.Max(end, span.end);
                }
            }

            _totalSpan = new TextSpan(start, end - start);

            _visitIntoStructuredTrivia =
                _nodeSet.Any(static n => n.IsPartOfStructuredTrivia()) ||
                _tokenSet.Any(static t => t.IsPartOfStructuredTrivia()) ||
                _triviaSet.Any(static t => t.IsPartOfStructuredTrivia());

            _shouldVisitTrivia = _triviaSet.Count > 0 || _visitIntoStructuredTrivia;
        }

        private bool ShouldVisit(TextSpan span) {
            if (!span.IntersectsWith(_totalSpan))
                return false;

            foreach (var s in _spanSet) {
                if (span.IntersectsWith(s))
                    return true;
            }

            return false;
        }

        internal override SyntaxNode Visit(SyntaxNode node) {
            var rewritten = node;

            if (node is not null) {
                var isReplacedNode = _nodeSet.Remove(node);

                if (isReplacedNode)
                    CalculateVisitationCriteria();

                if (ShouldVisit(node.fullSpan))
                    rewritten = base.Visit(node);

                if (isReplacedNode && _computeReplacementNode is not null)
                    rewritten = _computeReplacementNode((TNode)node, (TNode)rewritten);
            }

            return rewritten;
        }

        internal override SyntaxToken VisitToken(SyntaxToken token) {
            if (token is null)
                return null;

            var rewritten = token;
            var isReplacedToken = _tokenSet.Remove(token);

            if (isReplacedToken)
                CalculateVisitationCriteria();

            if (_shouldVisitTrivia && ShouldVisit(token.fullSpan))
                rewritten = base.VisitToken(token);

            if (isReplacedToken && _computeReplacementToken is not null)
                rewritten = _computeReplacementToken(token, rewritten);

            return rewritten;
        }

        internal override SyntaxTrivia VisitListElement(SyntaxTrivia trivia) {
            var rewritten = trivia;
            var isReplacedTrivia = _triviaSet.Remove(trivia);

            if (isReplacedTrivia)
                CalculateVisitationCriteria();

            if (visitIntoStructuredTrivia && trivia.hasStructure && ShouldVisit(trivia.fullSpan))
                rewritten = VisitTrivia(trivia);

            if (isReplacedTrivia && _computeReplacementTrivia is not null)
                rewritten = _computeReplacementTrivia(trivia, rewritten);

            return rewritten;
        }
    }
}
