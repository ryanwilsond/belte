using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// A builder for a <see cref="SyntaxList" />.
/// </summary>
internal sealed class SyntaxListBuilder {
    private ArrayElement<GreenNode?>[] _nodes;

    /// <summary>
    /// Creates a new <see cref="SyntaxListBuilder" /> with the given starting size.
    /// </summary>
    internal SyntaxListBuilder(int size) {
        _nodes = new ArrayElement<GreenNode?>[size];
    }

    /// <summary>
    /// Creates a <see cref="SyntaxListBuilder" /> with the default starting size of 8.
    /// </summary>
    internal static SyntaxListBuilder Create() {
        return new SyntaxListBuilder(8);
    }

    /// <summary>
    /// The number of items currently in the builder.
    /// </summary>
    internal int Count { get; private set; }

    /// <summary>
    /// Gets the node at the given index.
    /// </summary>
    internal GreenNode this[int index] {
        get {
            return _nodes[index];
        }
        set {
            _nodes[index].value = value;
        }
    }

    /// <summary>
    /// Clears the builder.
    /// </summary>
    internal void Clear() {
        Count = 0;
    }

    /// <summary>
    /// Adds a node to the end of the builder.
    /// </summary>
    internal void Add(GreenNode item) {
        if (item is null) return;

        if (item.isList) {
            var slotCount = item.slotCount;

            EnsureAdditionalCapacity(slotCount);

            for (var i = 0; i < slotCount; i++)
                Add(item.GetSlot(i));
        } else {
            EnsureAdditionalCapacity(1);
            _nodes[Count++].value = item;
        }
    }

    /// <summary>
    /// Adds an array of nodes to the end of the builder.
    /// </summary>
    internal void AddRange(GreenNode[] items) {
        AddRange(items, 0, items.Length);
    }

    /// <summary>
    /// Adds a subrange of an array to the end of the builder.
    /// </summary>
    internal void AddRange(GreenNode[] items, int offset, int length) {
        EnsureAdditionalCapacity(length - offset);

        for (var i = offset; i < length; i++)
            Add(items[i]);
    }

    /// <summary>
    /// Adds a <see cref="SyntaxList<GreenNode>" /> to the end of the builder.
    /// </summary>
    internal void AddRange(SyntaxList<GreenNode> list) {
        AddRange(list, 0, list.Count);
    }

    /// <summary>
    /// Adds a subrange of a <see cref="SyntaxList<GreenNode>" /> to the end of the builder.
    /// </summary>
    internal void AddRange(SyntaxList<GreenNode> list, int offset, int length) {
        EnsureAdditionalCapacity(length - offset);

        for (var i = offset; i < length; i++)
            Add(list[i]);
    }

    /// <summary>
    /// Adds a <see cref="SyntaxList<T>" /> to the end of the builder.
    /// </summary>
    internal void AddRange<T>(SyntaxList<T> list) where T : GreenNode {
        AddRange(list, 0, list.Count);
    }

    /// <summary>
    /// Adds a subrange of a <see cref="SyntaxList<T>" /> to the end of the builder.
    /// </summary>
    internal void AddRange<T>(SyntaxList<T> list, int offset, int length) where T : GreenNode {
        AddRange(new SyntaxList<GreenNode>(list.node), offset, length);
    }

    /// <summary>
    /// Removes the last item in the builder.
    /// </summary>
    internal void RemoveLast() {
        Count--;
        _nodes[Count].value = null;
    }

    internal bool Any(SyntaxKind kind) {
        for (var i = 0; i < Count; i++) {
            if (_nodes[i].value.kind == kind)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts the builder into a <see cref="GreenNode[]" />.
    /// </summary>
    internal GreenNode[] ToArray() {
        var array = new GreenNode[Count];

        for (var i = 0; i < array.Length; i++)
            array[i] = _nodes[i];

        return array;
    }

    /// <summary>
    /// Converts the builder into a type of list node depending on the current size.
    /// </summary>
    internal GreenNode ToListNode() {
        switch (Count) {
            case 0:
                return null;
            case 1:
                return _nodes[0];
            case 2:
                return SyntaxList.List(_nodes[0], _nodes[1]);
            case 3:
                // Can optimize and add a three child list if needed later
                return SyntaxList.List(new[] { _nodes[0], _nodes[1], _nodes[2] });
            default:
                var tmp = new ArrayElement<GreenNode>[Count];
                Array.Copy(_nodes, tmp, Count);
                return SyntaxList.List(tmp);
        }
    }

    /// <summary>
    /// Converts the builder into a <see cref="SyntaxList<GreenNode>" />.
    /// </summary>
    internal SyntaxList<GreenNode> ToList() {
        return new SyntaxList<GreenNode>(ToListNode());
    }

    /// <summary>
    /// Converts the builder into a <see cref="SyntaxList<T>" />.
    /// </summary>
    internal SyntaxList<T> ToList<T>() where T : GreenNode {
        return new SyntaxList<T>(ToListNode());
    }

    private void EnsureAdditionalCapacity(int additionalCount) {
        var currentSize = _nodes.Length;
        var requiredSize = Count + additionalCount;

        if (requiredSize <= currentSize) return;

        var newSize =
            requiredSize < 8 ? 8 :
            requiredSize >= (int.MaxValue / 2) ? int.MaxValue :
            Math.Max(requiredSize, currentSize * 2);

        Array.Resize(ref _nodes, newSize);
    }
}
