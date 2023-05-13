using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal sealed class SyntaxTrivia : BelteSyntaxNode {
    /// <param name="position">Position of the trivia (indexed by nodes, not by character).</param>
    /// <param name="text">Text associated with the trivia.</param>
    internal SyntaxTrivia(SyntaxKind kind, string text) : base(kind, text.Length) {
        this.text = text;
    }

    internal SyntaxTrivia(SyntaxKind kind, string text, Diagnostic[] diagnostics)
        : base(kind, text.Length, diagnostics) {
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

    internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
        return new SyntaxTrivia(kind, text, diagnostics);
    }

    internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) {
        return visitor.VisitTrivia(this);
    }

    internal override void Accept(SyntaxVisitor visitor) {
        visitor.VisitTrivia(this);
    }
}
