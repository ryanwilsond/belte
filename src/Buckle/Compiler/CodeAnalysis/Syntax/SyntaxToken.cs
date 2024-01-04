using System;
using Buckle.CodeAnalysis.Display;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a token in the syntax tree.
/// </summary>
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
    /// If the token has been fabricated by the compiler.
    /// </summary>
    public bool isFabricated => node?.isFabricated ?? false;

    /// <summary>
    /// The parent of this token.
    /// </summary>
    internal SyntaxNode parent { get; }

    /// <summary>
    /// The underlying token node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The absolute start position of this token in relation to a <see cref="SourceText" />.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// The slot index of this token in relation to the parent.
    /// </summary>
    internal int index { get; }

    /// <summary>
    /// The kind of token.
    /// </summary>
    internal SyntaxKind kind => node.kind;

    /// <summary>
    /// The value of the token, if any value exists.
    /// </summary>
    /// <returns></returns>
    internal object value => node.GetValue();

    /// <summary>
    /// The text of the token, if any text exists.
    /// </summary>
    /// <returns></returns>
    internal string text => ToString();

    /// <summary>
    /// The width of the token, excluding any trivia.
    /// </summary>
    internal int width => node?.width ?? 0;

    /// <summary>
    /// The full width of the token, including any trivia.
    /// </summary>
    internal int fullWidth => node?.fullWidth ?? 0;

    /// <summary>
    /// The span of the token, excluding any trivia.
    /// </summary>
    internal TextSpan span => node != null ? new TextSpan(position + node.GetLeadingTriviaWidth(), node.width) : null;

    /// <summary>
    /// The full span of the token, including any trivia.
    /// </summary>
    /// <returns></returns>
    internal TextSpan fullSpan => new TextSpan(position, fullWidth);

    /// <summary>
    /// The <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    internal SyntaxTree syntaxTree => parent?.syntaxTree;

    /// <summary>
    /// The absolute location of this token in the
    /// <see cref="SyntaxTree.text" /> of the <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    internal TextLocation location => syntaxTree != null ? new TextLocation(syntaxTree.text, span) : null;

    /// <summary>
    /// The leading trivia of this token, if any.
    /// </summary>
    internal SyntaxTriviaList leadingTrivia => node != null ?
        new SyntaxTriviaList(this, node.GetLeadingTrivia(), position)
        : null;

    /// <summary>
    /// The trailing trivia of this token, if any.
    /// </summary>
    internal SyntaxTriviaList trailingTrivia {
        get {
            if (node is null)
                return null;

            var leading = node.GetLeadingTrivia();
            var index = 0;

            if (leading != null)
                index = leading.isList ? leading.slotCount : 1;

            var trailingGreen = node.GetTrailingTrivia();
            var trailingPosition = position + fullWidth;

            if (trailingGreen != null)
                trailingPosition -= trailingGreen.fullWidth;

            return new SyntaxTriviaList(this, trailingGreen, trailingPosition, index);
        }
    }

    public override string ToString() {
        return node != null ? node.ToString() : string.Empty;
    }

    /// <summary>
    /// Write a pretty-print text representation of this <see cref="SyntaxToken" /> to an out.
    /// </summary>
    /// <param name="text">Out.</param>
    public void WriteTo(DisplayText text) {
        text.Write(CreatePunctuation("⟨"));
        // All tokens are tokens, so we don't need to display token every time
        text.Write(CreateIdentifier(kind.ToString().Replace("Token", "")));

        if (this.text != null) {
            text.Write(CreatePunctuation(", "));
            text.Write(CreateString($"\"{this.text}\""));
        }

        text.Write(CreatePunctuation("⟩"));
    }
}
