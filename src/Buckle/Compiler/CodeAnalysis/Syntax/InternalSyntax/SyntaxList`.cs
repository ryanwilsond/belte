using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of GreenNodes.
/// </summary>
internal sealed partial class SyntaxList<T> : IEquatable<SyntaxList<T>> where T : GreenNode {
    /// <summary>
    /// Creates a new <see cref="SyntaxList" />
    /// </summary>
    /// <param name="node"></param>
    internal SyntaxList(GreenNode node) {
        this.node = node;
    }

    /// <summary>
    /// The list node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The number of items.
    /// </summary>
    public int Count => node is null ? 0 : (node.isList ? node.slotCount : 1);

    /// <summary>
    /// Gets the item at the given index.
    /// </summary>
    public T this[int index] {
        get {
            if (node is null)
                return null;
            else if (node.isList)
                return (T)node.GetSlot(index);
            else if (index == 0)
                return (T)node;
            else
                throw ExceptionUtilities.Unreachable();
        }
    }

    public T last {
        get {
            var node = this.node;

            if (node.isList)
                return (T)node.GetSlot(node.slotCount - 1);

            return (T)node;
        }
    }

    public bool Any() {
        return node is not null;
    }

    public bool Any(SyntaxKind kind) {
        foreach (var element in this) {
            if (element.kind == kind)
                return true;
        }

        return false;
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    public static bool operator ==(SyntaxList<T> left, SyntaxList<T> right) {
        return left.node == right.node;
    }

    public static bool operator !=(SyntaxList<T> left, SyntaxList<T> right) {
        return left.node != right.node;
    }

    public bool Equals(SyntaxList<T> other) {
        return node == other.node;
    }

    public override bool Equals(object? obj) {
        return (obj is SyntaxList<T> list) && Equals(list);
    }

    public override int GetHashCode() {
        return node is not null ? node.GetHashCode() : 0;
    }

    internal void CopyTo(int offset, ArrayElement<GreenNode>[] array, int arrayOffset, int count) {
        for (var i = 0; i < count; i++)
            array[arrayOffset + i].value = GetRequiredItem(i + offset);
    }

    /// <summary>
    /// Returns this list as a <see cref="SeparatedSyntaxList<TOther>" />.
    /// </summary>
    internal SeparatedSyntaxList<TOther> AsSeparatedList<TOther>() where TOther : GreenNode {
        return new SeparatedSyntaxList<TOther>(this);
    }

    /// <summary>
    /// Gets the item at the given index.
    /// </summary>
    internal T GetRequiredItem(int index) {
        return this[index];
    }

    /// <summary>
    /// Gets the item at the given index, or if the list is a single item gets that item.
    /// </summary>
    internal GreenNode ItemUntyped(int index) {
        var node = this.node;

        if (node.isList)
            return node.GetSlot(index);

        return node;
    }

    public static implicit operator SyntaxList<T>(T node) {
        return new SyntaxList<T>(node);
    }

    public static implicit operator SyntaxList<T>(SyntaxList<GreenNode> nodes) {
        return new SyntaxList<T>(nodes.node);
    }

    public static implicit operator SyntaxList<GreenNode>(SyntaxList<T> nodes) {
        return new SyntaxList<GreenNode>(nodes.node);
    }
}
