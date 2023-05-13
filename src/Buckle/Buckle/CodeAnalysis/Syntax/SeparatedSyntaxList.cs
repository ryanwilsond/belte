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

    internal SeparatedSyntaxList(SyntaxNodeOrTokenList list) {
        int allCount = list.Count;
        Count = (allCount + 1) >> 1;
        _separatorCount = allCount >> 1;
        _list = list;
    }

    internal SeparatedSyntaxList(SyntaxNode node, int index) : this(new SyntaxNodeOrTokenList(node, index)) { }

    /// <summary>
    /// Number of non-separator SyntaxNodes in collection.
    /// </summary>
    public int Count { get; }

    internal TextSpan fullSpan => _list.fullSpan;

    internal TextSpan span => _list.span;

    /// <summary>
    /// Indexes SyntaxNodes in collection skipping separators.
    /// </summary>
    public T this[int index] {
        get {
            var node = _list.node;

            if (node != null) {
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
    /// Get a separator at an index. The index itself skips separators.
    /// </summary>
    /// <param name="index">Index of separator.</param>
    /// <returns>Separator <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken GetSeparator(int index) {
        var node = _list.node;

        if (node != null) {
            if (unchecked((uint)index < (uint)_separatorCount)) {
                index = (index << 1) + 1;
                var green = node.green.GetSlot(index);

                return new SyntaxToken(node.parent, green, node.GetChildPosition(index), _list.index + index);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal bool Any() {
        return _list.Any();
    }

    internal SyntaxNodeOrTokenList GetWithSeparators() => _list;

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
}
