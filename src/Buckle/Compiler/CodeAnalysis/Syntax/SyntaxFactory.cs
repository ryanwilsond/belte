
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Class used for handling the creation of red SyntaxNodes.
/// </summary>
internal static partial class SyntaxFactory {
    /// <summary>
    /// Creates a <see cref="ReferenceExpressionSyntax" /> with only a given name.
    /// </summary>
    internal static ReferenceExpressionSyntax Reference(string name) {
        return (ReferenceExpressionSyntax)InternalSyntax.SyntaxFactory.ReferenceExpression(
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.RefKeyword),
            InternalSyntax.SyntaxFactory.IdentifierName(
                InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name)
            )
        ).CreateRed();
    }
}
