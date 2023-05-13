using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class SyntaxList<T> where T : GreenNode {
    internal SyntaxList(GreenNode node) {
        this.node = node;
    }

    internal GreenNode node { get; }

    public int count => node == null ? 0 : (node.isList ? node.slotCount : 1);

    public T this[int index] {
        get {
            if (node == null)
                return null;
            else if (node.isList)
                return (T)node.GetSlot(index);
            else if (index == 0)
                return (T)node;
            else
                throw ExceptionUtilities.Unreachable();
        }
    }

    internal T GetRequiredItem(int index) {
        var node = this[index];
        return node;
    }

    internal GreenNode? ItemUntyped(int index) {
        var node = this.node;

        if (node.isList)
            return node.GetSlot(index);

        return node;
    }

    public bool Any() {
        return node != null;
    }

    public bool Any(SyntaxKind kind) {
        foreach (var element in this) {
            if (element.kind == kind)
                return true;
        }

        return false;
    }

    public T last {
        get {
            var node = this.node;

            if (node.isList)
                return (T)node.GetSlot(node.slotCount - 1);

            return (T)node;
        }
    }

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    internal SeparatedSyntaxList<TOther> AsSeparatedList<TOther>() where TOther : GreenNode {
        return new SeparatedSyntaxList<TOther>(this);
    }

    internal void CopyTo(int offset, ArrayElement<GreenNode>[] array, int arrayOffset, int count) {
        for (int i = 0; i < count; i++)
            array[arrayOffset + i].Value = GetRequiredItem(i + offset);
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
