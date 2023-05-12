using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal sealed class SyntaxTrivia : GreenNode {
    /// <param name="position">Position of the trivia (indexed by nodes, not by character).</param>
    /// <param name="text">Text associated with the trivia.</param>
    internal SyntaxTrivia(SyntaxKind kind, string text) : base(kind, text.Length) {
        this.text = text;
    }

    /// <summary>
    /// Text associated with the <see cref="SyntaxTrivia" />.
    /// </summary>
    internal string text { get; }

    internal override GreenNode GetSlot(int index) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
        throw ExceptionUtilities.Unreachable();
    }
}
