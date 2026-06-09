using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal class SyntaxListPool {
    private ArrayElement<SyntaxListBuilder>[] _freeList = new ArrayElement<SyntaxListBuilder>[8];
    private int _freeIndex;

    internal SyntaxListPool() { }

    internal SyntaxListBuilder Allocate() {
        SyntaxListBuilder item;
        if (_freeIndex > 0) {
            _freeIndex--;
            item = _freeList[_freeIndex].value;
            _freeList[_freeIndex].value = null;
        } else {
            item = new SyntaxListBuilder(8);
        }

        return item;
    }

    internal SyntaxListBuilder<TNode> Allocate<TNode>() where TNode : GreenNode {
        return new SyntaxListBuilder<TNode>(Allocate());
    }

    internal SyntaxList<SyntaxToken> ToTokenListAndFree(SyntaxListBuilder builder) {
        var listNode = builder.ToListNode();
        Free(builder);
        return new SyntaxList<SyntaxToken>(listNode);
    }

    internal void Free(SyntaxListBuilder item) {
        if (item is null)
            return;

        item.Clear();

        if (_freeIndex >= _freeList.Length)
            Grow();

        _freeList[_freeIndex].value = item;
        _freeIndex++;
    }

    private void Grow() {
        var tmp = new ArrayElement<SyntaxListBuilder>[_freeList.Length * 2];
        Array.Copy(_freeList, tmp, _freeList.Length);
        _freeList = tmp;
    }

    internal SyntaxList<TNode> ToListAndFree<TNode>(SyntaxListBuilder<TNode> item) where TNode : GreenNode {
        if (item.isNull)
            return default;

        var list = item.ToList();
        Free(item);
        return list;
    }

    internal SeparatedSyntaxList<TNode> ToSeparatedListAndFree<TNode>(SyntaxListBuilder<BelteSyntaxNode> builder)
        where TNode : GreenNode {
        var list = builder.ToList();
        Free(builder);
        return new SeparatedSyntaxList<TNode>(list);
    }
}
