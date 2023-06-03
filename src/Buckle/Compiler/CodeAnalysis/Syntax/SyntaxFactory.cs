
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Class used for handling the creation of red SyntaxNodes.
/// </summary>
internal static partial class SyntaxFactory {
    /// <summary>
    /// Creates a <see cref="ReferenceExpressionSyntax" /> with only a given name.
    /// </summary>
    internal static ReferenceExpressionSyntax Reference(string name) {
        return (ReferenceExpressionSyntax)new InternalSyntax.ReferenceExpressionSyntax(
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.RefKeyword),
            InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name)
        ).CreateRed();
    }

    internal static SyntaxToken Name(string name) {
        return new SyntaxToken(null, InternalSyntax.SyntaxFactory.Token(SyntaxKind.IdentifierToken, name), -1, -1);
    }
}
