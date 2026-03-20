using System.IO;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a token in the tree.
/// </summary>
internal partial class SyntaxToken : BelteSyntaxNode {
    private readonly string _text;
    private readonly GreenNode _leading;
    private readonly GreenNode _trailing;

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" />.
    /// </summary>
    /// <param name="fullWidth">A predefined full width for this token.</param>
    /// <param name="text">Text related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="value">Value related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="leadingTrivia"><see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).</param>
    /// <param name="trailingTrivia"><see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).</param>
    internal SyntaxToken(SyntaxKind kind, int fullWidth, string text, object value,
        GreenNode leadingTrivia, GreenNode trailingTrivia)
        : base(kind, fullWidth) {
        _text = text;
        this.value = value;
        _leading = leadingTrivia;
        _trailing = trailingTrivia;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with a predefined full width, text, a value, trivia, and diagnostics.
    /// </summary>
    // Because this constructor is called possibly thousands of times, the duplicate code is warranted for efficiency
    internal SyntaxToken(SyntaxKind kind, int fullWidth, string text, object value,
        GreenNode leadingTrivia, GreenNode trailingTrivia, Diagnostic[] diagnostics)
        : base(kind, fullWidth, diagnostics) {
        _text = text;
        this.value = value;
        _leading = leadingTrivia;
        _trailing = trailingTrivia;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with trivia.
    /// </summary>
    internal SyntaxToken(SyntaxKind kind, GreenNode leadingTrivia, GreenNode trailingTrivia)
        : base(kind) {
        if (leadingTrivia is not null) {
            AdjustFlagsAndWidth(leadingTrivia);
            _leading = leadingTrivia;
        }

        if (trailingTrivia is not null) {
            AdjustFlagsAndWidth(trailingTrivia);
            _trailing = trailingTrivia;
        }
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with trivia and diagnostics.
    /// </summary>
    internal SyntaxToken(SyntaxKind kind, GreenNode leadingTrivia, GreenNode trailingTrivia, Diagnostic[] diagnostics)
        : base(kind, diagnostics) {
        if (leadingTrivia is not null) {
            AdjustFlagsAndWidth(leadingTrivia);
            _leading = leadingTrivia;
        }

        if (trailingTrivia is not null) {
            AdjustFlagsAndWidth(trailingTrivia);
            _trailing = trailingTrivia;
        }
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with text, a value, and trivia.
    /// </summary>
    internal SyntaxToken(SyntaxKind kind, string text, object value, GreenNode leadingTrivia, GreenNode trailingTrivia)
        : base(kind) {
        this.value = value;
        _text = text;
        fullWidth = this.text.Length;

        if (leadingTrivia is not null) {
            AdjustFlagsAndWidth(leadingTrivia);
            _leading = leadingTrivia;
        }

        if (trailingTrivia is not null) {
            AdjustFlagsAndWidth(trailingTrivia);
            _trailing = trailingTrivia;
        }
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with text, a value, trivia, and diagnostics.
    /// </summary>
    internal SyntaxToken(
        SyntaxKind kind, string text, object value, GreenNode leadingTrivia,
        GreenNode trailingTrivia, Diagnostic[] diagnostics)
        : base(kind, diagnostics) {
        this.value = value;
        _text = text;
        fullWidth = this.text.Length;

        if (leadingTrivia is not null) {
            AdjustFlagsAndWidth(leadingTrivia);
            _leading = leadingTrivia;
        }

        if (trailingTrivia is not null) {
            AdjustFlagsAndWidth(trailingTrivia);
            _trailing = trailingTrivia;
        }
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with text and a value.
    /// </summary>
    internal SyntaxToken(SyntaxKind kind, string text, object value) : base(kind) {
        _text = text;
        fullWidth = this.text.Length;
        this.value = value;
    }

    /// <summary>
    /// Creates a missing <see cref="SyntaxToken" /> with trivia.
    /// </summary>
    internal static SyntaxToken CreateMissing(SyntaxKind kind, GreenNode leadingTrivia, GreenNode trailingTrivia) {
        return new MissingToken(kind, leadingTrivia, trailingTrivia);
    }

    /// <summary>
    /// Text related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal virtual string text => isFabricated ? "" : _text ?? SyntaxFacts.GetText(kind);

    /// <summary>
    /// Value related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal virtual object value { get; }

    internal override int width => text.Length;

    internal override bool isToken => true;

    /// <summary>
    /// <see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).
    /// </summary>
    internal SyntaxList<BelteSyntaxNode> leadingTrivia => new SyntaxList<BelteSyntaxNode>(GetLeadingTrivia());

    /// <summary>
    /// <see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).
    /// </summary>
    internal SyntaxList<BelteSyntaxNode> trailingTrivia => new SyntaxList<BelteSyntaxNode>(GetTrailingTrivia());

    public override string ToString() {
        return text;
    }

    internal override int GetLeadingTriviaWidth() {
        var leading = GetLeadingTrivia();
        return leading is not null ? leading.fullWidth : 0;
    }

    internal override int GetTrailingTriviaWidth() {
        var trailing = GetTrailingTrivia();
        return trailing is not null ? trailing.fullWidth : 0;
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

    internal override DirectiveStack ApplyDirectives(DirectiveStack stack) {
        if (containsDirectives) {
            stack = ApplyDirectivesToTrivia(GetLeadingTrivia(), stack);
            stack = ApplyDirectivesToTrivia(GetTrailingTrivia(), stack);
        }

        return stack;
    }

    internal static DirectiveStack ApplyDirectivesToTrivia(GreenNode triviaList, DirectiveStack stack) {
        if (triviaList is not null && triviaList.containsDirectives)
            return ApplyDirectivesToListOrNode(triviaList, stack);

        return stack;
    }

    /// <summary>
    /// Returns a new <see cref="SyntaxToken" /> identical to this one, with new leading trivia.
    /// </summary>
    internal virtual SyntaxToken TokenWithLeadingTrivia(GreenNode trivia) {
        return new SyntaxToken(kind, text, value, trivia, GetTrailingTrivia(), GetDiagnostics());
    }

    /// <summary>
    /// Returns a new <see cref="SyntaxToken" /> identical to this one, with new trailing trivia.
    /// </summary>
    internal virtual SyntaxToken TokenWithTrailingTrivia(GreenNode trivia) {
        return new SyntaxToken(kind, text, value, GetLeadingTrivia(), trivia, GetDiagnostics());
    }

    private protected override void WriteTokenTo(TextWriter writer, bool leading, bool trailing) {
        if (leading) {
            var trivia = GetLeadingTrivia();
            trivia?.WriteTo(writer, true, true);
        }

        writer.Write(text);

        if (trailing) {
            var trivia = GetTrailingTrivia();
            trivia?.WriteTo(writer, true, true);
        }
    }
}
