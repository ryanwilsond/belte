using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxToken : BelteSyntaxNode {
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
    }
}
