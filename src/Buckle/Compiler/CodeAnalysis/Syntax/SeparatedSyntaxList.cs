using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list separated by a common <see cref="SyntaxNode" />.
/// </summary>
/// <typeparam name="T">Child type of <see cref="SyntaxNode" />.</typeparam>
public sealed partial class SeparatedSyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    private readonly SyntaxNodeOrTokenList _list;
    private readonly int _separatorCount;

    /// <summary>
    /// Creates a new <see cref="SeparatedSyntaxList<T>" /> from an existing list.
    /// Treats every other node as a separator, starting on the second node from the given list.
    /// </summary>
    internal SeparatedSyntaxList(SyntaxNodeOrTokenList list) {
        var allCount = list.Count;
        Count = (allCount + 1) >> 1;
        _separatorCount = allCount >> 1;
        _list = list;
    }

    /// <summary>
    /// Creates a new <see cref="SeparatedSyntaxNode<T>" /> from an existing node.
    /// Converts the given node into a list before using it.
    /// The given index represents which child of the given node to treat as the beginning of the created
    /// <see cref="SeparatedSyntaxList<T>" />.
    /// </summary>
    internal SeparatedSyntaxList(SyntaxNode node, int index) : this(new SyntaxNodeOrTokenList(node, index)) { }

    /// <summary>
    /// Indexes SyntaxNodes in collection skipping separators.
    /// </summary>
    public T this[int index] {
        get {
            var node = _list.node;

            if (node is not null) {
                if (!node.isList) {
                    if (index == 0)
                        return (T)node;
                } else {
                    if (unchecked((uint)index < (uint)Count))
                        return (T)node.GetNodeSlot(index << 1);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>
    /// Number of non-separator SyntaxNodes in collection.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// The underlying node.
    /// </summary>
    internal SyntaxNode node => _list.node;

    /// <summary>
    /// The combined full span of all the contained items.
    /// </summary>
    internal TextSpan fullSpan => _list.fullSpan;

    /// <summary>
    /// The combined span of all the contained items.
    /// </summary>
    internal TextSpan span => _list.span;

    /// <summary>
    /// Get a separator at an index. The index itself skips separators.
    /// </summary>
    /// <param name="index">Index of separator.</param>
    /// <returns>Separator <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken GetSeparator(int index) {
        var node = _list.node;

        if (node is not null) {
            if (unchecked((uint)index < (uint)_separatorCount)) {
                index = (index << 1) + 1;
                var green = node.green.GetSlot(index);

                return new SyntaxToken(node.parent, green, node.GetChildPosition(index), _list.index + index);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>
    /// Returns the entire underlying list, including separators.
    /// </summary>
    /// <returns></returns>
    internal SyntaxNodeOrTokenList GetWithSeparators() => _list;

    internal bool Any() {
        return _list.Any();
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

    public static implicit operator SeparatedSyntaxList<SyntaxNode>(SeparatedSyntaxList<T> nodes) {
        return new SeparatedSyntaxList<SyntaxNode>(nodes._list);
    }
}
