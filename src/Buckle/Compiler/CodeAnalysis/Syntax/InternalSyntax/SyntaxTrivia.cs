using System.IO;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal class SyntaxTrivia : BelteSyntaxNode {
    /// <summary>
    /// Creates a new <see cref="SyntaxTrivia" />.
    /// </summary>
    /// <param name="text">Text associated with the trivia.</param>
    internal SyntaxTrivia(SyntaxKind kind, string text) : base(kind, text.Length) {
        this.text = text;

        if (kind == SyntaxKind.SkippedTokensTrivia)
            _flags |= NodeFlags.ContainsSkippedText;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxTrivia" /> with text and diagnostics.
    /// </summary>
    internal SyntaxTrivia(SyntaxKind kind, string text, Diagnostic[] diagnostics)
        : base(kind, text.Length, diagnostics) {
        this.text = text;

        if (kind == SyntaxKind.SkippedTokensTrivia)
            _flags |= NodeFlags.ContainsSkippedText;
    }

    internal override int width => fullWidth;

    internal override bool isTrivia => true;

    /// <summary>
    /// Text associated with the <see cref="SyntaxTrivia" />.
    /// </summary>
    internal string text { get; }

    public override string ToString() {
        return text;
    }

    internal override GreenNode GetSlot(int index) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
        return new SyntaxTrivia(kind, text, diagnostics);
    }

    internal override int GetLeadingTriviaWidth() {
        return 0;
    }

    internal override int GetTrailingTriviaWidth() {
        return 0;
    }

    internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) {
        return visitor.VisitTrivia(this);
    }

    internal override void Accept(SyntaxVisitor visitor) {
        visitor.VisitTrivia(this);
    }

    private protected override void WriteTriviaTo(TextWriter writer) {
        writer.Write(text);
    }
}
