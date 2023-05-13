using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal class SyntaxListBuilder {
    private ArrayElement<GreenNode?>[] _nodes;

    internal SyntaxListBuilder(int size) {
        _nodes = new ArrayElement<GreenNode?>[size];
    }

    internal int count { get; private set; }

    internal static SyntaxListBuilder Create() {
        return new SyntaxListBuilder(8);
    }

    internal void Clear() {
        count = 0;
    }

    internal GreenNode? this[int index] {
        get {
            return _nodes[index];
        }
        set {
            _nodes[index].Value = value;
        }
    }

    internal void Add(GreenNode? item) {
        if (item == null) return;

        if (item.isList) {
            int slotCount = item.slotCount;

            EnsureAdditionalCapacity(slotCount);

            for (int i = 0; i < slotCount; i++)
                Add(item.GetSlot(i));
        } else {
            EnsureAdditionalCapacity(1);
            _nodes[count++].Value = item;
        }
    }

    internal void AddRange(GreenNode[] items) {
        AddRange(items, 0, items.Length);
    }

    internal void AddRange(GreenNode[] items, int offset, int length) {
        EnsureAdditionalCapacity(length - offset);

        for (int i = offset; i < length; i++)
            Add(items[i]);
    }

    internal void AddRange(SyntaxList<GreenNode> list) {
        AddRange(list, 0, list.count);
    }

    internal void AddRange(SyntaxList<GreenNode> list, int offset, int length) {
        EnsureAdditionalCapacity(length - offset);

        for (int i = offset; i < length; i++)
            Add(list[i]);
    }

    internal void AddRange<T>(SyntaxList<T> list) where T : GreenNode {
        AddRange(list, 0, list.count);
    }

    internal void AddRange<T>(SyntaxList<T> list, int offset, int length) where T : GreenNode {
        AddRange(new SyntaxList<GreenNode>(list.node), offset, length);
    }

    internal void RemoveLast() {
        count--;
        _nodes[count].Value = null;
    }

    private void EnsureAdditionalCapacity(int additionalCount) {
        int currentSize = _nodes.Length;
        int requiredSize = count + additionalCount;

        if (requiredSize <= currentSize) return;

        int newSize =
            requiredSize < 8 ? 8 :
            requiredSize >= (int.MaxValue / 2) ? int.MaxValue :
            Math.Max(requiredSize, currentSize * 2);

        Array.Resize(ref _nodes, newSize);
    }

    internal bool Any(SyntaxKind kind) {
        for (int i = 0; i < count; i++) {
            if (_nodes[i].Value.kind == kind)
                return true;
        }

        return false;
    }

    internal GreenNode[] ToArray() {
        var array = new GreenNode[this.count];

        for (int i = 0; i < array.Length; i++)
            array[i] = _nodes[i];

        return array;
    }

    internal GreenNode? ToListNode() {
        switch (this.count) {
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
                var tmp = new ArrayElement<GreenNode>[this.count];
                Array.Copy(_nodes, tmp, this.count);
                return SyntaxList.List(tmp);
        }
    }

    internal SyntaxList<GreenNode> ToList() {
        return new SyntaxList<GreenNode>(ToListNode());
    }

    internal SyntaxList<T> ToList<T>() where T : GreenNode {
        return new SyntaxList<T>(ToListNode());
    }
}
