using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents list of SyntaxTrivias.
/// </summary>
public sealed partial class SyntaxTriviaList : IReadOnlyList<SyntaxTrivia> {
    /// <summary>
    /// Creates a new <see cref="SyntaxTriviaList" /> from an existing token, an underlying node, a position,
    /// and an optional start index.
    /// </summary>
    internal SyntaxTriviaList(SyntaxToken token, GreenNode node, int position, int index = 0) {
        this.token = token;
        this.node = node;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// The number of items in the list.
    /// </summary>
    public int Count => node is null ? 0 : (node.isList ? node.slotCount : 1);

    /// <summary>
    /// The token that this list is wrapping.
    /// </summary>
    internal SyntaxToken token { get; }

    /// <summary>
    /// The underlying list node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The position of this list.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// What index to start at in relation to the underlying list node.
    /// </summary>
    internal int index { get; }

    /// <summary>
    /// The combined full span of all the children. Should be the same as <see cref="span" />.
    /// </summary>
    internal TextSpan fullSpan => node is null ? null : new TextSpan(position, node.fullWidth);

    /// <summary>
    /// The combined span of all the children. Should be the same as <see cref="fullSpan" />.
    /// </summary>
    internal TextSpan span => node is null
        ? null
        : TextSpan.FromBounds(
            position + node.GetLeadingTriviaWidth(), position + node.fullWidth - node.GetTrailingTriviaWidth()
          );

    /// <summary>
    /// Gets the child trivia at the given index.
    /// </summary>
    public SyntaxTrivia this[int index] {
        get {
            if (node is not null) {
                if (node.isList) {
                    if (unchecked((uint)index < (uint)node.slotCount)) {
                        return new SyntaxTrivia(
                            token, node.GetSlot(index), position + node.GetSlotOffset(index), this.index + index
                        );
                    }
                } else if (index == 0) {
                    return new SyntaxTrivia(token, node, position, this.index);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public SyntaxTrivia First() {
        if (Any())
            return this[0];

        throw new InvalidOperationException();
    }

    public SyntaxTrivia Last() {
        if (Any())
            return this[Count - 1];

        throw new InvalidOperationException();
    }

    public bool Any() {
        return node is not null;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxTrivia> IEnumerable<SyntaxTrivia>.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (node is null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    private static GreenNode GetGreenNodeAt(GreenNode node, int i) {
        return node.isList ? node.GetSlot(i) : node;
    }
}
