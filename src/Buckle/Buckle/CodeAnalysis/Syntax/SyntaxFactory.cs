
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Class used for handling the creation of red SyntaxNodes.
/// </summary>
internal static partial class SyntaxFactory {
    internal static ReferenceExpressionSyntax Reference(string name) {
        return (ReferenceExpressionSyntax)new InternalSyntax.ReferenceExpressionSyntax(
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.ReferenceExpression),
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name)
        ).CreateRed();
    }
}
