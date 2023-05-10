using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Token type.
/// </summary>
public sealed class SyntaxToken : SyntaxNode {
    /// <param name="position">
    /// Position of <see cref="SyntaxToken" /> (indexed by the <see cref="SyntaxNode" />, not character
    /// in <see cref="SourceText" />).
    /// </param>
    /// <param name="text">Text related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="value">Value related to <see cref="SyntaxToken" /> (if applicable).</param>
    /// <param name="leadingTrivia"><see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).</param>
    /// <param name="trailingTrivia"><see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).</param>
    internal SyntaxToken(SyntaxTree syntaxTree, SyntaxKind kind, int position, string text, object value,
        ImmutableArray<SyntaxTrivia> leadingTrivia, ImmutableArray<SyntaxTrivia> trailingTrivia)
        : base(syntaxTree) {
        this.kind = kind;
        this.position = position;
        this.text = text;
        this.value = value;
        this.leadingTrivia = leadingTrivia;
        this.trailingTrivia = trailingTrivia;
    }

    public override SyntaxKind kind { get; }

    /// <summary>
    /// If this <see cref="SyntaxToken" /> was created artificially instead of coming from the
    /// <see cref="SourceText" />.
    /// </summary>
    public bool isFabricated => (flags & NodeFlags.IsMissing) != 0;

    /// <summary>
    /// Position of <see cref="SyntaxToken" /> (indexed by the <see cref="SyntaxNode" />, not character in
    /// <see cref="SourceText" />).
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// Text related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal string text { get; }

    /// <summary>
    /// Value related to <see cref="SyntaxToken" /> (if applicable).
    /// </summary>
    internal object value { get; }

    /// <summary>
    /// If the <see cref="SyntaxToken" /> contains any text from the <see cref="SourceText" /> that was skipped,
    /// in the form of bad token trivia.
    /// </summary>
    internal bool containsSkippedText => (flags & NodeFlags.ContainsSkippedText) != 0;

    internal override TextSpan span => new TextSpan(position, text?.Length ?? 0);

    internal override TextSpan fullSpan {
        get {
            var start = leadingTrivia.Length == 0 ? span.start : leadingTrivia.First().span.start;
            var end = trailingTrivia.Length == 0 ? span.end : trailingTrivia.Last().span.end;

            return TextSpan.FromBounds(start, end);
        }
    }

    /// <summary>
    /// <see cref="SyntaxTrivia" /> before <see cref="SyntaxToken" /> (anything).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> leadingTrivia { get; }

    /// <summary>
    /// <see cref="SyntaxTrivia" /> after <see cref="SyntaxToken" /> (same line).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> trailingTrivia { get; }

    /// <summary>
    /// Gets all child SyntaxNodes, which is none.
    /// </summary>
    public override IEnumerable<SyntaxNode> GetChildren() {
        return Array.Empty<SyntaxNode>();
    }
}
