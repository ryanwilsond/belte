using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxToken : BelteSyntaxNode {
    /// <summary>
    /// Represents a <see cref="SyntaxToken" /> that was fabricated by the <see cref="Parser" />.
    /// Because of this, it cannot have text or a value.
    /// </summary>
    internal class MissingToken : SyntaxToken {
        internal MissingToken(SyntaxKind kind, GreenNode leadingTrivia, GreenNode trailingTrivia)
            : base(kind, leadingTrivia, trailingTrivia) {
            this.flags |= NodeFlags.IsMissing;
        }

        internal MissingToken(
            SyntaxKind kind, GreenNode leadingTrivia, GreenNode trailingTrivia, Diagnostic[] diagnostics)
            : base(kind, leadingTrivia, trailingTrivia, diagnostics) {
            this.flags |= NodeFlags.IsMissing;
        }

        internal override string text => string.Empty;

        internal override object value => null;

        internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
            return new MissingToken(kind, _leading, _trailing, diagnostics);
        }

        internal override SyntaxToken TokenWithLeadingTrivia(GreenNode trivia) {
            return new MissingToken(kind, GetLeadingTrivia(), trivia, GetDiagnostics());
        }

        internal override SyntaxToken TokenWithTrailingTrivia(GreenNode trivia) {
            return new MissingToken(kind, trivia, GetTrailingTrivia(), GetDiagnostics());
        }
    }
}
