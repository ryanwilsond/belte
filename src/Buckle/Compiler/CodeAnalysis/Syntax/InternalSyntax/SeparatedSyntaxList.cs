using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of syntax with separators every other node, starting on the second node.
/// </summary>
internal sealed class SeparatedSyntaxList<T> : IEquatable<SeparatedSyntaxList<T>> where T : GreenNode {
    private readonly SyntaxList<GreenNode> _list;

    /// <summary>
    /// Creates a new <see cref="SeparatedSyntaxList<T>" /> from a <see cref="SyntaxList<GreenNode>" />.
    /// Treats every other node as a separator, starting on the second node.
    /// </summary>
    internal SeparatedSyntaxList(SyntaxList<GreenNode> list) {
        _list = list;
    }

    /// <summary>
    /// The underlying list node.
    /// </summary>
    internal GreenNode node => _list.node;

    /// <summary>
    /// The number of items, including separators.
    /// </summary>
    public int Count => (_list.Count + 1) >> 1;

    /// <summary>
    /// The number of separators.
    /// </summary>
    public int separatorCount => _list.Count >> 1;

    /// <summary>
    /// Gets a child at the given index, skipping over separators.
    /// </summary>
    public T this[int index] => (T)_list[index << 1];

    /// <summary>
    /// Gets a separator at the given index.
    /// </summary>
    public GreenNode GetSeparator(int index) {
        return _list[(index << 1) + 1];
    }

    /// <summary>
    /// Gets all children, including separators.
    /// </summary>
    public SyntaxList<GreenNode> GetWithSeparators() {
        return _list;
    }

    public static bool operator ==(in SeparatedSyntaxList<T> left, in SeparatedSyntaxList<T> right) {
        return left.Equals(right);
    }

    public static bool operator !=(in SeparatedSyntaxList<T> left, in SeparatedSyntaxList<T> right) {
        return !left.Equals(right);
    }

    public bool Equals(SeparatedSyntaxList<T> other) {
        return _list == other._list;
    }

    public override bool Equals(object? obj) {
        return (obj is SeparatedSyntaxList<T> list) && Equals(list);
    }

    public override int GetHashCode() {
        return _list.GetHashCode();
    }

    public static implicit operator SeparatedSyntaxList<GreenNode>(SeparatedSyntaxList<T> list) {
        return new SeparatedSyntaxList<GreenNode>(list.GetWithSeparators());
    }
}
