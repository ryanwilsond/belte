using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    private abstract class BaseListEditor : SyntaxRewriter {
        private readonly TextSpan _elementSpan;
        private readonly bool _visitTrivia;
        private readonly bool _visitIntoStructuredTrivia;

        private protected readonly ListEditKind _editKind;

        internal BaseListEditor(
            TextSpan elementSpan,
            ListEditKind editKind,
            bool visitTrivia,
            bool visitIntoStructuredTrivia) {
            _elementSpan = elementSpan;
            _editKind = editKind;
            _visitTrivia = visitTrivia || visitIntoStructuredTrivia;
            _visitIntoStructuredTrivia = visitIntoStructuredTrivia;
        }

        internal override bool visitIntoStructuredTrivia => _visitIntoStructuredTrivia;

        private bool ShouldVisit(TextSpan span) {
            if (span.IntersectsWith(_elementSpan))
                return true;

            return false;
        }

        internal override SyntaxNode Visit(SyntaxNode node) {
            var rewritten = node;

            if (node is not null) {
                if (ShouldVisit(node.fullSpan))
                    rewritten = base.Visit(node);
            }

            return rewritten;
        }

        internal override SyntaxToken VisitToken(SyntaxToken token) {
            var rewritten = token;

            if (_visitTrivia && ShouldVisit(token.fullSpan))
                rewritten = base.VisitToken(token);

            return rewritten;
        }

        internal override SyntaxTrivia VisitListElement(SyntaxTrivia trivia) {
            var rewritten = trivia;

            if (visitIntoStructuredTrivia && trivia.hasStructure && ShouldVisit(trivia.fullSpan))
                rewritten = VisitTrivia(trivia);

            return rewritten;
        }
    }
}
