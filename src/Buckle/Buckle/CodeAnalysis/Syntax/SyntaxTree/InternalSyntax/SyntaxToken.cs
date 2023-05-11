using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Token type.
/// </summary>
internal sealed class SyntaxToken : GreenNode {
    private string _text;

    /// <param name="position">
    /// Position of <see cref="SyntaxToken" /> (indexed by the <see cref="SyntaxNode" />, not character
    /// in <see cref="SourceText" />).
    /// </param>
    /// <param name="text">Text related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="value">Value related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="leadingTrivia"><see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).</param>
    /// <param name="trailingTrivia"><see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).</param>
    internal SyntaxToken(SyntaxKind kind, int fullWidth, string text, object value,
        SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        : base(kind, fullWidth) {
        _text = text;
        this.value = value;
        this.leadingTrivia = leadingTrivia;
        this.trailingTrivia = trailingTrivia;
    }

    /// <summary>
    /// If this <see cref="SyntaxToken" /> was created artificially instead of coming from the
    /// <see cref="SourceText" />.
    /// </summary>
    internal bool isFabricated => (flags & NodeFlags.IsMissing) != 0;

    /// <summary>
    /// Position of <see cref="SyntaxToken" /> (indexed by the <see cref="SyntaxNode" />, not character in
    /// <see cref="SourceText" />).
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// Text related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal string text => _text ?? SyntaxFacts.GetText(kind);

    /// <summary>
    /// Value related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal object value { get; }

    /// <summary>
    /// The width of the <see cref="SyntaxToken" />, not including any leading or trailing trivia.
    /// </summary>
    internal override int width => text.Length;

    /// <summary>
    /// If the <see cref="SyntaxToken" /> contains any text from the <see cref="SourceText" /> that was skipped,
    /// in the form of bad token trivia.
    /// </summary>
    internal bool containsSkippedText => (flags & NodeFlags.ContainsSkippedText) != 0;

    /// <summary>
    /// <see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).
    /// </summary>
    internal SyntaxTriviaList leadingTrivia { get; }

    /// <summary>
    /// <see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).
    /// </summary>
    internal SyntaxTriviaList trailingTrivia { get; }

    internal override int GetLeadingTriviaWidth() {
        return leadingTrivia != null ? leadingTrivia.fullWidth : 0;
    }

    internal override int GetTrailingTriviaWidth() {
        return trailingTrivia != null ? trailingTrivia.fullWidth : 0;
    }

    internal override SyntaxTriviaList GetLeadingTrivia() {
        return leadingTrivia;
    }

    internal override SyntaxTriviaList GetTrailingTrivia() {
        return trailingTrivia;
    }

    internal override object GetValue() {
        return value;
    }

    internal override GreenNode GetSlot(int index) {
        throw ExceptionUtilities.Unreachable();
    }
}
