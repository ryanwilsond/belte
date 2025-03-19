using System;
using System.Diagnostics;
using System.IO;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a token in the syntax tree.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public sealed class SyntaxToken {
    /// <summary>
    /// A predicate function that checks if the passed <see cref="SyntaxToken" /> has a width greater than zero.
    /// </summary>
    internal static readonly Func<SyntaxToken, bool> NonZeroWidth = t => t.width > 0;

    /// <summary>
    /// A predicate function that always returns true. Used as a default predicate function.
    /// </summary>
    internal static readonly Func<SyntaxToken, bool> Any = t => true;

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with a parent, underlying token, and an absolute position.
    /// </summary>
    internal SyntaxToken(SyntaxNode parent, GreenNode token, int position, int index) {
        this.parent = parent;
        node = token;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken"/> from an underlying token.
    /// </summary>
    internal SyntaxToken(GreenNode token) {
        node = token;
    }

    /// <summary>
    /// If the token has been fabricated by the compiler.
    /// </summary>
    public bool isFabricated => node?.isFabricated ?? false;

    /// <summary>
    /// The parent of this token.
    /// </summary>
    public SyntaxNode parent { get; }

    /// <summary>
    /// The underlying token node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The absolute start position of this token in relation to a <see cref="SourceText" />.
    /// </summary>
    public int position { get; }

    /// <summary>
    /// The slot index of this token in relation to the parent.
    /// </summary>
    public int index { get; }

    /// <summary>
    /// The kind of token.
    /// </summary>
    public SyntaxKind kind => node.kind;

    /// <summary>
    /// The value of the token, if any value exists.
    /// </summary>
    /// <returns></returns>
    public object value => node.GetValue();

    /// <summary>
    /// The text of the token, if any text exists.
    /// </summary>
    /// <returns></returns>
    public string text => ToString();

    /// <summary>
    /// The width of the token, excluding any trivia.
    /// </summary>
    public int width => node?.width ?? 0;

    /// <summary>
    /// The full width of the token, including any trivia.
    /// </summary>
    public int fullWidth => node?.fullWidth ?? 0;

    /// <summary>
    /// The span of the token, excluding any trivia.
    /// </summary>
    public TextSpan span => node is not null ? new TextSpan(position + node.GetLeadingTriviaWidth(), node.width) : null;

    /// <summary>
    /// The full span of the token, including any trivia.
    /// </summary>
    /// <returns></returns>
    public TextSpan fullSpan => new TextSpan(position, fullWidth);

    /// <summary>
    /// The <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    public SyntaxTree syntaxTree => parent?.syntaxTree;

    /// <summary>
    /// The absolute location of this token in the
    /// <see cref="SyntaxTree.text" /> of the <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    public TextLocation location => syntaxTree is not null ? new TextLocation(syntaxTree.text, span) : null;

    /// <summary>
    /// Determines whether this token has any leading trivia.
    /// </summary>
    public bool hasLeadingTrivia => leadingTrivia.Count > 0;

    /// <summary>
    /// Determines whether this token has any trailing trivia.
    /// </summary>
    public bool hasTrailingTrivia => trailingTrivia.Count > 0;

    /// <summary>
    /// The leading trivia of this token, if any.
    /// </summary>
    public SyntaxTriviaList leadingTrivia => node is not null ?
        new SyntaxTriviaList(this, node.GetLeadingTrivia(), position)
        : null;

    /// <summary>
    /// The trailing trivia of this token, if any.
    /// </summary>
    public SyntaxTriviaList trailingTrivia {
        get {
            if (node is null)
                return null;

            var leading = node.GetLeadingTrivia();
            var index = 0;

            if (leading is not null)
                index = leading.isList ? leading.slotCount : 1;

            var trailingGreen = node.GetTrailingTrivia();
            var trailingPosition = position + fullWidth;

            if (trailingGreen is not null)
                trailingPosition -= trailingGreen.fullWidth;

            return new SyntaxTriviaList(this, trailingGreen, trailingPosition, index);
        }
    }

    public SyntaxToken GetNextToken(bool includeZeroWidth = false, bool includeSkipped = false) {
        if (node is null)
            return default;

        return SyntaxNavigator.Instance.GetNextToken(this, includeZeroWidth, includeSkipped);
    }

    public override string ToString() {
        return node is not null ? node.ToString() : "";
    }

    public void WriteTo(TextWriter writer) {
        node?.WriteTo(writer);
    }

    public static bool operator ==(SyntaxToken left, SyntaxToken right) {
        return left.Equals(right);
    }

    public static bool operator !=(SyntaxToken left, SyntaxToken right) {
        return !left.Equals(right);
    }

    public bool Equals(SyntaxToken other) {
        if (other is null)
            return false;

        return parent == other.parent &&
               node == other.node &&
               position == other.position &&
               index == other.index;
    }

    public override bool Equals(object obj) {
        return obj is SyntaxToken token && Equals(token);
    }

    public override int GetHashCode() {
        return Hash.Combine(parent, Hash.Combine(node, Hash.Combine(position, index)));
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + (node is not null ? node.kind : "None") + " " + ToString();
    }
}
