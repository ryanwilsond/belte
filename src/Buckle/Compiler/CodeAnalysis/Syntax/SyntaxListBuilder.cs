using System;

namespace Buckle.CodeAnalysis.Syntax;

internal sealed class SyntaxListBuilder {
    private ArrayElement<GreenNode>[] _nodes;

    internal SyntaxListBuilder(int size) {
        _nodes = new ArrayElement<GreenNode>[size];
    }

    internal int count { get; private set; }

    internal void Clear() {
        count = 0;
    }

    internal void Add(SyntaxNode item) {
        if (count >= _nodes.Length)
            Grow(count == 0 ? 8 : _nodes.Length * 2);

        _nodes[count++].value = item.green;
    }

    internal void AddRange<TNode>(SyntaxList<TNode> list, int offset, int count) where TNode : SyntaxNode {
        AddRange(new SyntaxList<SyntaxNode>(list.node), offset, count);
    }

    internal void AddRange(SyntaxList<SyntaxNode> list, int offset, int count) {
        if (this.count + count > _nodes.Length)
            Grow(this.count + count);

        var dst = this.count;

        for (int i = offset, limit = offset + count; i < limit; i++) {
            _nodes[dst].value = list.ItemInternal(i).green;
            dst++;
        }

        this.count += count;
    }

    internal void AddRange(SyntaxNodeOrTokenList list, int offset, int count) {
        if (this.count + count > _nodes.Length)
            Grow(this.count + count);

        var dst = this.count;

        for (int i = offset, limit = offset + count; i < limit; i++) {
            _nodes[dst].value = list[i].underlyingNode;
            dst++;
        }

        this.count += count;
    }

    internal void AddInternal(GreenNode item) {
        if (count >= _nodes.Length)
            Grow(count == 0 ? 8 : _nodes.Length * 2);

        _nodes[count++].value = item;
    }

    internal GreenNode ToListNode() {
        switch (count) {
            case 0:
                return null;
            case 1:
                return _nodes[0].value;
            case 2:
                return InternalSyntax.SyntaxList.List(_nodes[0].value, _nodes[1].value);
            default:
                var temp = new ArrayElement<GreenNode>[count];

                for (var i = 0; i < count; i++)
                    temp[i].value = _nodes[i].value;

                return InternalSyntax.SyntaxList.List(temp);
        }
    }

    private void Grow(int size) {
        var temp = new ArrayElement<GreenNode>[size];
        Array.Copy(_nodes, temp, _nodes.Length);
        _nodes = temp;
    }
}
