using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Token type.
/// </summary>
internal sealed class Token : Node {
    /// <param name="position">Position of token (indexed by the node, not character in source text)</param>
    /// <param name="text">Text related to token (if applicable)</param>
    /// <param name="value">Value related to token (if applicable)</param>
    /// <param name="leadingTrivia">Trivia before token (anything)</param>
    /// <param name="trailingTrivia">Trivia after token (same line)</param>
    internal Token(SyntaxTree syntaxTree, SyntaxType type, int position, string text, object value,
        ImmutableArray<SyntaxTrivia> leadingTrivia, ImmutableArray<SyntaxTrivia> trailingTrivia)
        : base(syntaxTree) {
        this.type = type;
        this.position = position;
        this.text = text;
        this.value = value;
        this.leadingTrivia = leadingTrivia;
        this.trailingTrivia = trailingTrivia;
    }

    internal override SyntaxType type { get; }

    /// <summary>
    /// Position of token (indexed by the node, not character in source text).
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// Text related to token (if applicable).
    /// </summary>
    internal string text { get; }

    /// <summary>
    /// Value related to token (if applicable).
    /// </summary>
    internal object value { get; }

    /// <summary>
    /// If token was created artificially, or if it came from the source text.
    /// </summary>
    internal bool isMissing => text == null;

    internal override TextSpan span => new TextSpan(position, text?.Length ?? 0);

    internal override TextSpan fullSpan {
        get {
            var start = leadingTrivia.Length == 0 ? span.start : leadingTrivia.First().span.start;
            var end = trailingTrivia.Length == 0 ? span.end : trailingTrivia.Last().span.end;
            return TextSpan.FromBounds(start, end);
        }
    }

    /// <summary>
    /// Trivia before token (anything).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> leadingTrivia { get; }

    /// <summary>
    /// Trivia after token (same line).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> trailingTrivia { get; }

    /// <summary>
    /// Gets all child nodes, which is none.
    /// </summary>
    internal override IEnumerable<Node> GetChildren() {
        return Array.Empty<Node>();
    }
}
