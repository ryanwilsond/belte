
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents an object that can walk through a <see cref="BelteSyntaxNode" /> without knowing the structure
/// of the <see cref="BelteSyntaxNode" />. Each type of node must implement it's own accept methods so it can tell
/// the <see cref="SyntaxVisitor" /> how to walk that type of node.
/// </summary>
internal abstract partial class SyntaxVisitor {
    /// <summary>
    /// Visits a <see cref="BelteSyntaxNode" />.
    /// </summary>
    internal virtual void Visit(BelteSyntaxNode node) {
        if (node == null)
            return;

        node.Accept(this);
    }

    /// <summary>
    /// Visits a <see cref="SyntaxToken" />.
    /// </summary>
    internal virtual void VisitToken(SyntaxToken token) {
        DefaultVisit(token);
    }

    /// <summary>
    /// Visits a <see cref="SyntaxTrivia" />.
    /// </summary>
    internal virtual void VisitTrivia(SyntaxTrivia trivia) {
        DefaultVisit(trivia);
    }

    /// <summary>
    /// The default visit method used if no other visit method is specified. Does nothing.
    /// </summary>
    internal virtual void DefaultVisit(BelteSyntaxNode node) { }
}
