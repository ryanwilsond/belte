using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a list of <typeparam name="T" />, and is not itself a <see cref="SyntaxNode" />.
/// </summary>
public sealed partial class SyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> from an underlying list node.
    /// </summary>
    internal SyntaxList(SyntaxNode node) {
        this.node = node;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> from an underlying list node.
    /// </summary>
    internal SyntaxList(T node) : this((SyntaxNode)node) { }

    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> from a list of nodes.
    /// </summary>
    internal SyntaxList(IEnumerable<T> nodes) : this(CreateNode(nodes)) { }

    /// <summary>
    /// Gets the child node at the given position from the underlying list node.
    /// </summary>
    public T this[int index] {
        get {
            if (node is not null) {
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

    /// <summary>
    /// The number of children in the list.
    /// </summary>
    public int Count => node is null ? 0 : (node.isList ? node.slotCount : 1);

    /// <summary>
    /// The underlying list node.
    /// </summary>
    internal SyntaxNode node { get; }

    /// <summary>
    /// The combined full span of all the children.
    /// </summary>
    internal TextSpan fullSpan => Count == 0
        ? null
        : TextSpan.FromBounds(this[0].fullSpan.start, this[Count - 1].fullSpan.end);

    /// <summary>
    /// The combined span of all the children.
    /// </summary>
    internal TextSpan span => Count == 0
        ? null
        : TextSpan.FromBounds(this[0].span.start, this[Count - 1].span.end);

    /// <summary>
    /// Gets the child at the given position.
    /// </summary>
    internal SyntaxNode ItemInternal(int index) {
        if (node?.isList == true)
            return node.GetNodeSlot(index);

        return node;
    }

    internal bool Any() {
        return node is not null;
    }

    private static SyntaxNode CreateNode(IEnumerable<T> nodes) {
        if (nodes is null)
            return null;

        var collection = nodes as ICollection<T>;
        var builder = (collection is not null)
            ? new SyntaxListBuilder<T>(collection.Count)
            : SyntaxListBuilder<T>.Create();

        foreach (var node in nodes)
            builder.Add(node);

        return builder.ToList().node;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() {
        if (Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<T>();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<T>();
    }

    public static implicit operator SyntaxList<SyntaxNode>(SyntaxList<T> nodes) {
        return new SyntaxList<SyntaxNode>(nodes.node);
    }

    public static explicit operator SyntaxList<T>(SyntaxList<SyntaxNode> nodes) {
        return new SyntaxList<T>(nodes.node);
    }
}
