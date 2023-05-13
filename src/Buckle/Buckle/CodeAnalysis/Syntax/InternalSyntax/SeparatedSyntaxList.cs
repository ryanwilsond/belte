
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal readonly struct SeparatedSyntaxList<T> where T : GreenNode {
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

    public static implicit operator SeparatedSyntaxList<GreenNode>(SeparatedSyntaxList<T> list) {
        return new SeparatedSyntaxList<GreenNode>(list.GetWithSeparators());
    }
}
