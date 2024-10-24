using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a list of SyntaxNodeOrTokens.
/// </summary>
public sealed partial class SyntaxNodeOrTokenList : IReadOnlyCollection<SyntaxNodeOrToken> {
    internal readonly int index;

    /// <summary>
    /// Creates a new <see cref="SyntaxNodeOrTokenList" /> from an underlying node.
    /// The given index represents which child of the given node to treat as the beginning of the created
    /// <see cref="SyntaxNodeOrTokenList" />.
    /// </summary>
    internal SyntaxNodeOrTokenList(SyntaxNode node, int index) {
        if (node is not null) {
            this.node = node;
            this.index = index;
        }
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxNodeOrTokenList" /> from a list of SyntaxNodeOrTokens.
    /// </summary>
    internal SyntaxNodeOrTokenList(IEnumerable<SyntaxNodeOrToken> nodesAndTokens)
        : this(CreateNode(nodesAndTokens), 0) { }

    /// <summary>
    /// The number of items in the list.
    /// </summary>
    public int Count => node is null ? 0 : node.green.isList ? node.slotCount : 1;

    /// <summary>
    /// Gets the child at the given index.
    /// </summary>
    public SyntaxNodeOrToken this[int index] {
        get {
            if (node is not null) {
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

    /// <summary>
    /// The underlying list node.
    /// </summary>
    internal SyntaxNode node { get; }

    /// <summary>
    /// The start position of this <see cref="SyntaxNodeOrTokenList" />.
    /// </summary>
    internal int position => node?.position ?? 0;

    /// <summary>
    /// The parent of the underlying list node.
    /// </summary>
    internal SyntaxNode parent => node?.parent;

    /// <summary>
    /// The combined full span of all the children.
    /// </summary>
    internal TextSpan fullSpan => node?.fullSpan;

    /// <summary>
    /// The combined span of all the children.
    /// </summary>
    internal TextSpan span => node?.span;

    internal SyntaxNodeOrToken First() {
        return this[0];
    }

    internal SyntaxNodeOrToken FirstOrDefault() {
        return Any()
            ? this[0]
            : null;
    }

    internal SyntaxNodeOrToken Last() {
        return this[Count - 1];
    }

    internal SyntaxNodeOrToken LastOrDefault() {
        return Any()
            ? this[Count - 1]
            : null;
    }

    internal bool Any() {
        return node is not null;
    }

    internal void CopyTo(int offset, GreenNode[] array, int arrayOffset, int count) {
        for (var i = 0; i < count; i++)
            array[arrayOffset + i] = this[i + offset].underlyingNode;
    }

    internal Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxNodeOrToken> IEnumerable<SyntaxNodeOrToken>.GetEnumerator() {
        return node is null
            ? new EmptyEnumerator<SyntaxNodeOrToken>()
            : GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return node is null
            ? new EmptyEnumerator<SyntaxNodeOrToken>()
            : GetEnumerator();
    }

    private static SyntaxNode CreateNode(IEnumerable<SyntaxNodeOrToken> nodesAndTokens) {
        ArgumentNullException.ThrowIfNull(nodesAndTokens, nameof(nodesAndTokens));
        var builder = new SyntaxNodeOrTokenListBuilder(8);
        builder.Add(nodesAndTokens);
        return builder.ToList().node;
    }
}
