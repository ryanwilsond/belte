using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxToken {
    internal class SyntaxLiteralExtended : SyntaxToken {
        internal SyntaxLiteralExtended(
            SyntaxKind kind, string text, string suffix, object value,
            GreenNode leadingTrivia, GreenNode trailingTrivia, Diagnostic[] diagnostics)
            : base(SyntaxKind.ExtendedLiteralToken, text, value, leadingTrivia, trailingTrivia, diagnostics) {
            this.suffix = suffix;
            contextualKind = kind;
        }

        internal string suffix { get; }

        internal override SyntaxKind contextualKind { get; }
    }
}
