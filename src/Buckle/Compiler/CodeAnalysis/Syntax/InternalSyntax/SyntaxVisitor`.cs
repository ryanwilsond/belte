
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a <see cref="SyntaxVisitor" /> that returns results from the visiting.
/// </summary>
internal abstract partial class SyntaxVisitor<TResult> {
    /// <summary>
    /// Visits a <see cref="BelteSyntaxNode" />.
    /// </summary>
    internal virtual TResult Visit(BelteSyntaxNode node) {
        if (node is null)
            return default;

        return node.Accept(this);
    }

    /// <summary>
    /// Visits a <see cref="SyntaxToken" />.
    /// </summary>
    internal virtual TResult VisitToken(SyntaxToken token) {
        return DefaultVisit(token);
    }

    /// <summary>
    /// Visits a <see cref="SyntaxTrivia" />.
    /// </summary>
    internal virtual TResult VisitTrivia(SyntaxTrivia trivia) {
        return DefaultVisit(trivia);
    }

    /// <summary>
    /// The default visit method used if no other visit method is specified. Does nothing.
    /// </summary>
    private protected virtual TResult DefaultVisit(BelteSyntaxNode node) {
        return default;
    }
}
