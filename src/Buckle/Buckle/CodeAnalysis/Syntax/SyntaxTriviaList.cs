using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents list of SyntaxTrivias.
/// </summary>
public sealed partial class SyntaxTriviaList : IReadOnlyList<SyntaxTrivia> {
    internal SyntaxTriviaList(SyntaxToken token, GreenNode node, int position, int index = 0) {
        this.token = token;
        this.node = node;
        this.position = position;
        this.index = index;
    }

    public int Count => node == null ? 0 : (node.isList ? node.slotCount : 1);

    internal SyntaxToken token { get; }

    internal GreenNode node { get; }

    internal int position { get; }

    internal int index { get; }

    internal TextSpan fullSpan => node == null ? null : new TextSpan(position, node.fullWidth);

    internal TextSpan span => node == null
        ? null
        : TextSpan.FromBounds(
            position + node.GetLeadingTriviaWidth(), position + node.fullWidth - node.GetTrailingTriviaWidth()
          );

    public SyntaxTrivia this[int index] {
        get {
            if (node != null) {
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

    internal SyntaxTrivia ElementAt(int index) {
        return this[index];
    }


    internal SyntaxTrivia First() {
        if (Any())
            return this[0];

        throw new InvalidOperationException();
    }

    internal SyntaxTrivia Last() {
        if (Any())
            return this[Count - 1];

        throw new InvalidOperationException();
    }

    internal bool Any() {
        return node != null;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxTrivia> IEnumerable<SyntaxTrivia>.GetEnumerator() {
        if (node == null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (node == null)
            return new EmptyEnumerator<SyntaxTrivia>();

        return new EnumeratorImpl(this);
    }

    private static GreenNode GetGreenNodeAt(GreenNode node, int i) {
        return node.isList ? node.GetSlot(i) : node;
    }
}
