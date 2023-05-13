using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Token type.
/// </summary>
internal sealed class SyntaxToken : BelteSyntaxNode {
    private string _text;
    private GreenNode _leading;
    private GreenNode _trailing;

    /// <param name="position">
    /// Position of <see cref="SyntaxToken" /> (indexed by the <see cref="SyntaxNode" />, not character
    /// in <see cref="SourceText" />).
    /// </param>
    /// <param name="text">Text related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="value">Value related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="leadingTrivia"><see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).</param>
    /// <param name="trailingTrivia"><see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).</param>
    internal SyntaxToken(SyntaxKind kind, int fullWidth, string text, object value,
        GreenNode leadingTrivia, GreenNode trailingTrivia)
        : base(kind, fullWidth) {
        _text = text;
        this.value = value;
        this._leading = leadingTrivia;
        this._trailing = trailingTrivia;
    }

    internal SyntaxToken(SyntaxKind kind, int fullWidth, string text, object value,
        GreenNode leadingTrivia, GreenNode trailingTrivia, Diagnostic[] diagnostics)
        : base(kind, fullWidth, diagnostics) {
        _text = text;
        this.value = value;
        this._leading = leadingTrivia;
        this._trailing = trailingTrivia;
    }

    internal SyntaxToken(SyntaxKind kind, string text, object value, GreenNode leadingTrivia, GreenNode trailingTrivia)
        : base(kind) {
        this.value = value;
        _text = text;
        fullWidth = text.Length;
        AdjustFlagsAndWidth(leadingTrivia);
        this._leading = leadingTrivia;
        AdjustFlagsAndWidth(trailingTrivia);
        this._trailing = trailingTrivia;
    }

    internal SyntaxToken(
        SyntaxKind kind, string text, object value, GreenNode leadingTrivia,
        GreenNode trailingTrivia, Diagnostic[] diagnostics)
        : base(kind, diagnostics) {
        this.value = value;
        _text = text;
        fullWidth = text.Length;
        AdjustFlagsAndWidth(leadingTrivia);
        this._leading = leadingTrivia;
        AdjustFlagsAndWidth(trailingTrivia);
        this._trailing = trailingTrivia;
    }

    internal SyntaxToken(SyntaxKind kind, string text, object value) : base(kind) {
        fullWidth = text.Length;
        _text = text;
        this.value = value;
    }

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
    /// <see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).
    /// </summary>
    internal SyntaxList<BelteSyntaxNode> leadingTrivia => new SyntaxList<BelteSyntaxNode>(GetLeadingTrivia());

    /// <summary>
    /// <see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).
    /// </summary>
    internal SyntaxList<BelteSyntaxNode> trailingTrivia => new SyntaxList<BelteSyntaxNode>(GetTrailingTrivia());

    internal override int GetLeadingTriviaWidth() {
        var leading = GetLeadingTrivia();
        return leading != null ? leading.fullWidth : 0;
    }

    internal override int GetTrailingTriviaWidth() {
        var trailing = GetTrailingTrivia();
        return trailing != null ? trailing.fullWidth : 0;
    }

    internal override GreenNode GetLeadingTrivia() {
        return _leading;
    }

    internal override GreenNode GetTrailingTrivia() {
        return _trailing;
    }

    internal override object GetValue() {
        return value;
    }

    internal override GreenNode GetSlot(int index) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override GreenNode WithLeadingTrivia(GreenNode trivia) {
        return TokenWithLeadingTrivia(trivia);
    }

    internal override GreenNode WithTrailingTrivia(GreenNode trivia) {
        return TokenWithTrailingTrivia(trivia);
    }

    internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
        return new SyntaxToken(kind, fullWidth, text, value, GetLeadingTrivia(), GetTrailingTrivia(), diagnostics);
    }

    internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) {
        return visitor.VisitToken(this);
    }

    internal override void Accept(SyntaxVisitor visitor) {
        visitor.VisitToken(this);
    }

    internal SyntaxToken TokenWithLeadingTrivia(GreenNode trivia) {
        var token = new SyntaxToken(kind, text, value, trivia, GetTrailingTrivia());
        token.flags |= flags;
        return token;
    }

    internal SyntaxToken TokenWithTrailingTrivia(GreenNode trivia) {
        var token = new SyntaxToken(kind, text, value, GetLeadingTrivia(), trivia);
        token.flags |= flags;
        return token;
    }
}
