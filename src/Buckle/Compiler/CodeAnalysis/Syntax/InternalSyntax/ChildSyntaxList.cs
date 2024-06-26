
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents and list of child nodes and tokens.
/// </summary>
internal sealed partial class ChildSyntaxList {
    private readonly GreenNode _node;
    private int _count;

    /// <summary>
    /// Creates a new <see cref="ChildSyntaxList" /> from an underlying node.
    /// </summary>
    internal ChildSyntaxList(GreenNode node) {
        _node = node;
        _count = -1;
    }

    /// <summary>
    /// The number of children.
    /// </summary>
    internal int Count {
        get {
            if (_count == -1)
                _count = CountNodes();

            return _count;
        }
    }

    internal Enumerator GetEnumerator() {
        return new Enumerator(_node);
    }

    internal Reversed Reverse() {
        return new Reversed(_node);
    }

    private int CountNodes() {
        var n = 0;
        var enumerator = GetEnumerator();

        while (enumerator.MoveNext())
            n++;

        return n;
    }
}
