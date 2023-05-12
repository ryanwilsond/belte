using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxNodeOrTokenList : IReadOnlyCollection<SyntaxNodeOrToken> {
    internal readonly int index;

    internal SyntaxNodeOrTokenList(SyntaxNode node, int index) {
        if (node != null) {
            this.node = node;
            this.index = index;
        }
    }

    internal SyntaxNodeOrTokenList(IEnumerable<SyntaxNodeOrToken> nodesAndTokens)
        : this(CreateNode(nodesAndTokens), 0) { }

    public int Count => node == null ? 0 : node.green.isList ? node.slotCount : 1;

    public SyntaxNodeOrToken this[int index] {
        get {
            if (node != null) {
                if (!node.isList) {
                    if (index == 0)
                        return node;
                } else {
                    if (unchecked((uint)index < (uint)node.slotCount)) {
                        var green = node.green.GetSlot(index);

                        if (green.isToken)
                            return new SyntaxToken(parent, green, node.GetChildPosition(index), this.index + index);

                        return node.GetNodeSlot(index);
                    }
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    internal SyntaxNode node { get; }

    internal int position => node?.position ?? 0;

    internal SyntaxNode parent => node?.parent;

    internal TextSpan fullSpan => node?.fullSpan;

    internal TextSpan span => node?.span;

    internal SyntaxNodeOrToken First() {
        return this[0];
    }

    internal SyntaxNodeOrToken FirstOrDefault() {
        return this.Any()
            ? this[0]
            : null;
    }

    internal SyntaxNodeOrToken Last() {
        return this[this.Count - 1];
    }

    internal SyntaxNodeOrToken LastOrDefault() {
        return this.Any()
            ? this[this.Count - 1]
            : null;
    }

    internal bool Any() {
        return node != null;
    }

    internal void CopyTo(int offset, GreenNode[] array, int arrayOffset, int count) {
        for (int i = 0; i < count; i++)
            array[arrayOffset + i] = this[i + offset].underlyingNode;
    }

    internal Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxNodeOrToken> IEnumerable<SyntaxNodeOrToken>.GetEnumerator() {
        return node == null
            ? new EmptyEnumerator<SyntaxNodeOrToken>()
            : GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return node == null
            ? new EmptyEnumerator<SyntaxNodeOrToken>()
            : GetEnumerator();
    }


    private static SyntaxNode CreateNode(IEnumerable<SyntaxNodeOrToken> nodesAndTokens) {
        if (nodesAndTokens == null)
            throw new ArgumentNullException(nameof(nodesAndTokens));

        var builder = new SyntaxNodeOrTokenListBuilder(8);
        builder.Add(nodesAndTokens);
        return builder.ToList().node;
    }
}
