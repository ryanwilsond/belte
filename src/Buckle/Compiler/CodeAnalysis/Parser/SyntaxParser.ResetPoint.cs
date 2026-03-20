
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxParser {
    private protected readonly struct ResetPoint {
        internal readonly int position;
        internal readonly GreenNode prevTokenTrailingTrivia;

        internal ResetPoint(
            int position,
            GreenNode prevTokenTrailingTrivia) {
            this.position = position;
            this.prevTokenTrailingTrivia = prevTokenTrailingTrivia;
        }
    }
}
