using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class SeparatedSyntaxList<T> : IEquatable<SeparatedSyntaxList<T>> where T : GreenNode {
    private readonly SyntaxList<GreenNode> _list;

    internal SeparatedSyntaxList(SyntaxList<GreenNode> list) {
        _list = list;
    }

    internal GreenNode node => _list.node;

    public int count => (_list.count + 1) >> 1;

    public int separatorCount => _list.count >> 1;

    public T this[int index] => (T)_list[index << 1];

    public GreenNode GetSeparator(int index) {
        return _list[(index << 1) + 1];
    }

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
        return (obj is SeparatedSyntaxList<T>) && Equals((SeparatedSyntaxList<T>)obj);
    }

    public override int GetHashCode() {
        return _list.GetHashCode();
    }

    public static implicit operator SeparatedSyntaxList<GreenNode>(SeparatedSyntaxList<T> list) {
        return new SeparatedSyntaxList<GreenNode>(list.GetWithSeparators());
    }
}
