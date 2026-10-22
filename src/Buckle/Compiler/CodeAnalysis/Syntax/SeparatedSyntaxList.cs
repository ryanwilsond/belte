using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public int SeparatorCount {
        get {
            return _separatorCount;
        }
    }

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

    internal bool Any(Func<T, bool> predicate) {
        for (var i = 0; i < Count; i++) {
            if (predicate(this[i]))
                return true;
        }

        return false;
    }

    internal bool Any(SyntaxKind kind) {
        return IndexOf(kind) >= 0;
    }

    internal int IndexOf(SyntaxKind kind) {
        for (int i = 0, n = Count; i < n; i++) {
            if (this[i].kind == kind)
                return i;
        }

        return -1;
    }

    internal int IndexOf(T node) {
        for (int i = 0, n = Count; i < n; i++) {
            if (Equals(this[i], node))
                return i;
        }

        return -1;
    }

    internal SeparatedSyntaxList<T> ReplaceRange(T nodeInList, IEnumerable<T> newNodes) {
        ArgumentNullException.ThrowIfNull(newNodes);

        var index = IndexOf(nodeInList);

        if (index >= 0 && index < Count) {
            var newNodeList = newNodes.ToList();

            if (newNodeList.Count == 0)
                return Remove(nodeInList);

            var listWithFirstReplaced = Replace(nodeInList, newNodeList[0]);

            if (newNodeList.Count > 1) {
                newNodeList.RemoveAt(0);
                return listWithFirstReplaced.InsertRange(index + 1, newNodeList);
            }

            return listWithFirstReplaced;
        }

        throw new ArgumentOutOfRangeException(nameof(nodeInList));
    }

    internal SeparatedSyntaxList<T> Replace(T nodeInList, T newNode) {
        ArgumentNullException.ThrowIfNull(newNode);

        var index = IndexOf(nodeInList);

        if (index >= 0 && index < Count)
            return new SeparatedSyntaxList<T>(GetWithSeparators().Replace(nodeInList, newNode));

        throw new ArgumentOutOfRangeException(nameof(nodeInList));
    }

    internal SeparatedSyntaxList<T> Remove(T node) {
        var nodesAndSeparators = GetWithSeparators();
        var index = nodesAndSeparators.IndexOf(node);

        if (index >= 0 && index <= nodesAndSeparators.Count) {
            nodesAndSeparators = nodesAndSeparators.RemoveAt(index);

            if (index < nodesAndSeparators.Count && nodesAndSeparators[index].isToken)
                nodesAndSeparators = nodesAndSeparators.RemoveAt(index);
            else if (index > 0 && nodesAndSeparators[index - 1].isToken)
                nodesAndSeparators = nodesAndSeparators.RemoveAt(index - 1);

            return new SeparatedSyntaxList<T>(nodesAndSeparators);
        }

        return this;
    }

    internal SeparatedSyntaxList<T> InsertRange(int index, IEnumerable<T> nodes) {
        ArgumentNullException.ThrowIfNull(nodes);

        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var nodesAndSeparators = GetWithSeparators();
        var insertionIndex = index < Count ? nodesAndSeparators.IndexOf(this[index]) : nodesAndSeparators.Count;

        if (insertionIndex > 0 && insertionIndex < nodesAndSeparators.Count) {
            var previous = nodesAndSeparators[insertionIndex - 1];

            if (previous.isToken && !KeepSeparatorWithPreviousNode(previous.AsToken()))
                insertionIndex--;
        }

        var nodesToInsertWithSeparators = new List<SyntaxNodeOrToken>();

        foreach (var item in nodes) {
            if (item is not null) {
                if (nodesToInsertWithSeparators.Count > 0 ||
                    (insertionIndex > 0 && nodesAndSeparators[insertionIndex - 1].isNode)) {
                    nodesToInsertWithSeparators.Add(item.green.CreateSeparator(item));
                }

                nodesToInsertWithSeparators.Add(item);
            }
        }

        if (insertionIndex < nodesAndSeparators.Count && nodesAndSeparators[insertionIndex]
                is { isNode: true } nodeOrToken) {
            var node = nodesAndSeparators[insertionIndex].AsNode();
            nodesToInsertWithSeparators.Add(node.green.CreateSeparator(node));
        }

        return new SeparatedSyntaxList<T>(nodesAndSeparators.InsertRange(insertionIndex, nodesToInsertWithSeparators));
    }

    private static bool KeepSeparatorWithPreviousNode(in SyntaxToken separator) {
        foreach (var tr in separator.trailingTrivia) {
            if (tr.green.IsTriviaWithEndOfLine())
                return true;
        }

        return false;
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
