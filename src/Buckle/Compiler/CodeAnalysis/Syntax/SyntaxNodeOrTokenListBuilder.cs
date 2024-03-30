using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

internal class SyntaxNodeOrTokenListBuilder {
    private GreenNode[] _nodes;

    internal SyntaxNodeOrTokenListBuilder(int size) {
        _nodes = new GreenNode[size];
        count = 0;
    }

    internal static SyntaxNodeOrTokenListBuilder Create() {
        return new SyntaxNodeOrTokenListBuilder(8);
    }

    internal int count { get; private set; }

    internal void Clear() {
        count = 0;
    }

    internal SyntaxNodeOrToken this[int index] {
        get {
            var innerNode = _nodes[index];

            if (innerNode.isToken)
                return new SyntaxNodeOrToken(null, innerNode, 0, 0);
            else
                return innerNode.CreateRed();
        }
        set {
            _nodes[index] = value.underlyingNode;
        }
    }

    internal void Add(GreenNode item) {
        if (count >= _nodes.Length)
            Grow(count == 0 ? 8 : _nodes.Length * 2);

        _nodes[count++] = item;
    }

    internal void Add(SyntaxNode item) {
        Add(item.green);
    }

    internal void Add(SyntaxToken item) {
        Add(item.node);
    }

    internal void Add(SyntaxNodeOrToken item) {
        Add(item.underlyingNode);
    }

    internal void Add(SyntaxNodeOrTokenList list) {
        Add(list, 0, list.Count);
    }

    internal void Add(SyntaxNodeOrTokenList list, int offset, int length) {
        if (count + length > _nodes.Length)
            Grow(count + length);

        list.CopyTo(offset, _nodes, count, length);
        count += length;
    }

    internal void Add(IEnumerable<SyntaxNodeOrToken> nodeOrTokens) {
        foreach (var n in nodeOrTokens)
            Add(n);
    }

    internal void RemoveLast() {
        count--;
        _nodes[count] = null;
    }

    private void Grow(int size) {
        var tmp = new GreenNode[size];
        Array.Copy(_nodes, tmp, _nodes.Length);
        _nodes = tmp;
    }

    internal SyntaxNodeOrTokenList ToList() {
        if (count > 0) {
            switch (count) {
                case 1:
                    if (_nodes[0].isToken) {
                        return new SyntaxNodeOrTokenList(
                            InternalSyntax.SyntaxList.List(new[] { _nodes[0] }).CreateRed(),
                            index: 0);
                    } else {
                        return new SyntaxNodeOrTokenList(_nodes[0].CreateRed(), index: 0);
                    }
                case 2:
                    return new SyntaxNodeOrTokenList(
                        InternalSyntax.SyntaxList.List(new[] { _nodes[0], _nodes[1] }).CreateRed(),
                        index: 0);
                case 3:
                    return new SyntaxNodeOrTokenList(
                        InternalSyntax.SyntaxList.List(new[] { _nodes[0], _nodes[1], _nodes[2] }).CreateRed(),
                        index: 0);
                default:
                    var tmp = new ArrayElement<GreenNode>[count];

                    for (var i = 0; i < count; i++)
                        tmp[i].value = _nodes[i]!;

                    return new SyntaxNodeOrTokenList(InternalSyntax.SyntaxList.List(tmp).CreateRed(), index: 0);
            }
        } else {
            return null;
        }
    }
}
