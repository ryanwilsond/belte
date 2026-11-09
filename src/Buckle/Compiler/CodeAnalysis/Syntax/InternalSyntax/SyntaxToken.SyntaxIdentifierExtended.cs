using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxToken {
    internal class SyntaxIdentifierExtended : SyntaxToken {
        internal SyntaxIdentifierExtended(
            SyntaxKind contextualKind,
            string text,
            object value,
            GreenNode leadingTrivia,
            GreenNode trailingTrivia,
            Diagnostic[] diagnostics,
            SyntaxAnnotation[] annotations)
            : base(
                SyntaxKind.IdentifierToken,
                text,
                value,
                leadingTrivia,
                trailingTrivia,
                diagnostics,
                annotations
            ) {
            this.contextualKind = contextualKind;
        }

        internal override SyntaxKind contextualKind { get; }
    }
}
