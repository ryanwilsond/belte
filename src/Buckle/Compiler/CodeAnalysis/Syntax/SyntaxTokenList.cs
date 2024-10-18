using System;
using System.Collections;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a read-only list of SyntaxTokens.
/// </summary>
public sealed partial class SyntaxTokenList : IReadOnlyList<SyntaxToken> {
    private readonly SyntaxNode _parent;
    private readonly int _index;

    internal static SyntaxTokenList Empty => new SyntaxTokenListBuilder(0).ToList();

    /// <summary>
    /// Creates a <see cref="SyntaxTokenList" /> from an underlying <see cref="GreenNode" />.
    /// </summary>
    internal SyntaxTokenList(SyntaxNode parent, GreenNode tokenOrList, int position, int index) {
        _parent = parent;
        node = tokenOrList;
        this.position = position;
        _index = index;
    }

    public SyntaxTokenList(SyntaxToken token) {
        _parent = token.parent;
        node = token.node;
        position = token.position;
        _index = 0;
    }

    public SyntaxTokenList(params SyntaxToken[] tokens) : this(null, CreateNode(tokens), 0, 0) { }

    public SyntaxTokenList(IEnumerable<SyntaxToken> tokens) : this(null, CreateNode(tokens), 0, 0) { }

    private static GreenNode CreateNode(SyntaxToken[] tokens) {
        if (tokens is null)
            return null;

        var builder = new SyntaxTokenListBuilder(tokens.Length);

        for (var i = 0; i < tokens.Length; i++) {
            var node = tokens[i].node;
            builder.Add(node);
        }

        return builder.ToList().node;
    }

    private static GreenNode CreateNode(IEnumerable<SyntaxToken> tokens) {
        if (tokens is null)
            return null;

        var builder = SyntaxTokenListBuilder.Create();

        foreach (var token in tokens)
            builder.Add(token.node);

        return builder.ToList().node;
    }

    internal GreenNode node { get; }

    internal int position { get; }

    public int Count => node is null ? 0 : (node.isList ? node.slotCount : 1);

    public SyntaxToken this[int index] {
        get {
            if (node is not null) {
                if (node.isList) {
                    if (unchecked((uint)index < (uint)node.slotCount)) {
                        return new SyntaxToken(
                            _parent, node.GetSlot(index), position + node.GetSlotOffset(index), _index + index
                        );
                    }
                } else if (index == 0) {
                    return new SyntaxToken(_parent, node, position, _index);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public TextSpan fullSpan {
        get {
            if (node is null)
                return null;

            return new TextSpan(position, node.fullWidth);
        }
    }

    public TextSpan span {
        get {
            if (node is null)
                return null;

            return TextSpan.FromBounds(position + node.GetLeadingTriviaWidth(),
                position + node.fullWidth - node.GetTrailingTriviaWidth()
            );
        }
    }

    public bool Any() {
        return node is not null;
    }

    internal void CopyTo(int offset, GreenNode[] array, int arrayOffset, int count) {
        for (var i = 0; i < count; i++)
            array[arrayOffset + i] = GetGreenNodeAt(offset + i);
    }

    private GreenNode GetGreenNodeAt(int i) {
        return GetGreenNodeAt(node, i);
    }

    private static GreenNode GetGreenNodeAt(GreenNode node, int i) {
        return node.isList ? node.GetSlot(i) : node;
    }

    public int IndexOf(SyntaxToken tokenInList) {
        for (int i = 0, n = Count; i < n; i++) {
            var token = this[i];

            if (token == tokenInList)
                return i;
        }

        return -1;
    }

    internal int IndexOf(SyntaxKind syntaxKind) {
        for (int i = 0, n = Count; i < n; i++) {
            if (this[i].kind == syntaxKind)
                return i;
        }

        return -1;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SyntaxToken> IEnumerable<SyntaxToken>.GetEnumerator() {
        if (Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<SyntaxToken>();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        if (Any())
            return new EnumeratorImpl(this);

        return new EmptyEnumerator<SyntaxToken>();
    }

    public static SyntaxTokenList Create(SyntaxToken token) {
        return new SyntaxTokenList(token);
    }
}
