using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    internal SyntaxList(SyntaxNode node) {
        this.node = node;
    }

    internal SyntaxList(T node) : this((SyntaxNode)node) { }

    public int Count => node == null ? 0 : (node.isList ? node.slotCount : 1);

    internal SyntaxNode node { get; }

    public T this[int index] {
        get {
            if (node != null) {
                if (node.isList) {
                    if (unchecked((uint)index < (uint)node.slotCount))
                        return (T)node.GetNodeSlot(index);
                } else if (index == 0) {
                    return (T)node;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    internal TextSpan fullSpan => Count == 0
        ? null
        : TextSpan.FromBounds(this[0].fullSpan.start, this[Count - 1].fullSpan.end);

    internal TextSpan span => Count == 0
        ? null
        : TextSpan.FromBounds(this[0].span.start, this[Count - 1].span.end);

    internal bool Any() {
        return node != null;
    }

    internal SyntaxNode ItemInternal(int index) {
        if (node?.isList == true)
            return node.GetNodeSlot(index);

        return node;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() {
        if (this.Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<T>();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (this.Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<T>();
    }
}
